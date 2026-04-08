using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Employees;

public class EmployeeManagerAppService : ApplicationService
{
    private readonly IRepository<EmployeeManager, Guid> _repo;
    private readonly IRepository<Employee, Guid> _empRepo;

    public EmployeeManagerAppService(
        IRepository<EmployeeManager, Guid> repo,
        IRepository<Employee, Guid> empRepo)
    {
        _repo = repo;
        _empRepo = empRepo;
    }

    private static string Rel(ManagerRelationType t) => t.ToString();

    public async Task<Guid> AssignAsync(AssignManagerDto input)
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing");

        if (input.EmployeeId == input.ManagerEmployeeId)
            throw new BusinessException("EmployeeCannotManageSelf");

        if (!await _empRepo.AnyAsync(e => e.Id == input.EmployeeId))
            throw new BusinessException("EmployeeNotFound");

        if (!await _empRepo.AnyAsync(e => e.Id == input.ManagerEmployeeId))
            throw new BusinessException("ManagerNotFound");

        var relationType = Rel(input.RelationType);

        if (await _repo.AnyAsync(x => x.EmployeeId == input.EmployeeId && x.ManagerEmployeeId == input.ManagerEmployeeId &&
            x.RelationType == relationType && x.EndDate == null))  // Se esiste già un legame attivo identico, errore (evitiamo duplicati)
        {
            throw new BusinessException("ActiveAssignmentAlreadyExists");
        }

        // Se IsPrimary: spegni altri primary attivi dello stesso tipo
        if (input.IsPrimary)
        {
            var primaries = await _repo.GetListAsync(x =>
                x.EmployeeId == input.EmployeeId &&
                x.RelationType == relationType &&
                x.IsPrimary &&
                x.EndDate == null);

            foreach (var p in primaries) p.IsPrimary = false;

            foreach (var p in primaries)
                await _repo.UpdateAsync(p, autoSave: true);
        }

        var entity = ObjectMapper.Map<AssignManagerDto, EmployeeManager>(input);
        entity.TenantId = CurrentTenant.Id;
        entity.RelationType = relationType;
        entity.StartDate = input.StartDate;
        entity.EndDate = input.EndDate;

        await _repo.InsertAsync(entity, autoSave: true);
        return entity.Id;
    }

    public async Task<EmployeeDto[]> GetManagersAsync(Guid employeeId, ManagerRelationType relationType, bool onlyActive = true)
    {
        var rel = Rel(relationType);

        var links = await _repo.GetListAsync(x =>
            x.EmployeeId == employeeId &&
            x.RelationType == rel &&
            (!onlyActive || x.EndDate == null));

        var managerIds = links.Select(x => x.ManagerEmployeeId).Distinct().ToList();
        var managers = await _empRepo.GetListAsync(x => managerIds.Contains(x.Id));

        return managers.Select(ObjectMapper.Map<Employee, EmployeeDto>).ToArray();
    }

    public async Task<EmployeeDto[]> GetSubordinatesAsync(Guid managerEmployeeId, ManagerRelationType relationType, bool onlyActive = true)
    {
        var rel = Rel(relationType);

        var links = await _repo.GetListAsync(x =>
            x.ManagerEmployeeId == managerEmployeeId &&
            x.RelationType == rel &&
            (!onlyActive || x.EndDate == null));

        var empIds = links.Select(x => x.EmployeeId).Distinct().ToList();
        var emps = await _empRepo.GetListAsync(x => empIds.Contains(x.Id));

        return emps.Select(ObjectMapper.Map<Employee, EmployeeDto>).ToArray();
    }
    public async Task EndAssignmentAsync(EndAssignmentDto input)
    {
        if (CurrentTenant.Id == null)
            throw new BusinessException("TenantMissing");

        var relationType = input.RelationType.ToString();
        var endDate = input.EndDate ?? DateOnly.FromDateTime(Clock.Now);

        // prende l'assegnazione "attiva" (EndDate NULL) più recente per StartDate
        var activeLinks = await _repo.GetListAsync(x =>
            x.EmployeeId == input.EmployeeId &&
            x.ManagerEmployeeId == input.ManagerEmployeeId &&
            x.RelationType == relationType &&
            x.EndDate == null);

        if (activeLinks.Count == 0)
            return; // niente da chiudere (idempotente)

        // se ce ne sono più di una (anomalia), chiudiamo tutte con la stessa EndDate
        foreach (var link in activeLinks)
        {
            // regola date: EndDate non può essere prima di StartDate (se StartDate presente)
            if (link.StartDate.HasValue && endDate < link.StartDate.Value)
                throw new BusinessException("EndDateBeforeStartDate")
                    .WithData("StartDate", link.StartDate.Value.ToString())
                    .WithData("EndDate", endDate.ToString());

            link.EndDate = endDate;
            link.IsPrimary = false; // opzionale: un rapporto chiuso non è più primary
            await _repo.UpdateAsync(link, autoSave: true);
        }
    }
    public async Task<EmployeeManagerAssignmentDto[]> GetActiveAssignmentsAsync(
    Guid employeeId,
    ManagerRelationType? relationType = null,
    DateOnly? asOfDate = null)
    {
        var date = asOfDate ?? DateOnly.FromDateTime(Clock.Now);

        // filtro per tipo (se richiesto)
        var relString = relationType?.ToString();

        // attivo alla data = StartDate <= date AND (EndDate IS NULL OR EndDate >= date)
        var links = await _repo.GetListAsync(x =>
            x.EmployeeId == employeeId &&
            (relString == null || x.RelationType == relString) &&
            (!x.StartDate.HasValue || x.StartDate.Value <= date) &&
            (!x.EndDate.HasValue || x.EndDate.Value >= date));

        if (links.Count == 0)
            return Array.Empty<EmployeeManagerAssignmentDto>();

        var managerIds = links.Select(x => x.ManagerEmployeeId).Distinct().ToList();
        var managers = await _empRepo.GetListAsync(e => managerIds.Contains(e.Id));
        var managerById = managers.ToDictionary(x => x.Id);

        var result = new List<EmployeeManagerAssignmentDto>(links.Count);

        foreach (var link in links.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.RelationType))
        {
            if (!managerById.TryGetValue(link.ManagerEmployeeId, out var mgr))
                continue;

            // parse string -> enum (se c’è un valore inatteso, fallback)
            _ = Enum.TryParse<ManagerRelationType>(link.RelationType, out var relEnum);

            result.Add(new EmployeeManagerAssignmentDto
            {
                EmployeeId = link.EmployeeId,
                ManagerEmployeeId = link.ManagerEmployeeId,
                RelationType = relEnum,
                IsPrimary = link.IsPrimary,
                StartDate = link.StartDate,
                EndDate = link.EndDate,
                ManagerFullName = mgr.FullName,
                ManagerMatricola = mgr.Matricola
            });
        }

        return result.ToArray();
    }
}
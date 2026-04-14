using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Process;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminCycleParticipantAppService : ApplicationService, IAdminCycleParticipantAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;
    private readonly IRepository<ProcessTemplate, Guid> _templateRepository;

    public AdminCycleParticipantAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<ProcessPhase, Guid> phaseRepository,
        IRepository<ProcessTemplate, Guid> templateRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _employeeRepository = employeeRepository;
        _phaseRepository = phaseRepository;
        _templateRepository = templateRepository;
    }

    public async Task<CycleParticipantAdminDto> GetAsync(Guid? cycleId = null)
    {
        EnsureTenantAndAdmin();

        var cycleQuery = await _cycleRepository.GetQueryableAsync();
        var cycles = await _asyncExecuter.ToListAsync(
            cycleQuery
                .OrderByDescending(x => x.CycleYear)
                .ThenBy(x => x.Name));

        var selectedCycle = ResolveSelectedCycle(cycles, cycleId);

        var templateIds = cycles.Select(x => x.TemplateId).Distinct().ToList();
        var templateQuery = await _templateRepository.GetQueryableAsync();
        var templates = await _asyncExecuter.ToListAsync(
            templateQuery.Where(x => templateIds.Contains(x.Id)));
        var templateById = templates.ToDictionary(x => x.Id, x => x);

        var phaseQuery = await _phaseRepository.GetQueryableAsync();
        var phases = selectedCycle is null
            ? new List<ProcessPhase>()
            : await _asyncExecuter.ToListAsync(
                phaseQuery
                    .Where(x => x.TemplateId == selectedCycle.TemplateId)
                    .OrderBy(x => x.PhaseOrder)
                    .ThenBy(x => x.Code));
        var phaseById = phases.ToDictionary(x => x.Id, x => x);

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employees = await _asyncExecuter.ToListAsync(
            employeeQuery
                .Where(x => x.IsActive)
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.FullName));
        var employeeById = employees.ToDictionary(x => x.Id, x => x);

        var participants = new List<CycleParticipant>();
        if (selectedCycle is not null)
        {
            var participantQuery = await _participantRepository.GetQueryableAsync();
            participants = await _asyncExecuter.ToListAsync(
                participantQuery
                    .Where(x => x.CycleId == selectedCycle.Id)
                    .OrderBy(x => x.EmployeeId));
        }

        return new CycleParticipantAdminDto
        {
            SelectedCycleId = selectedCycle?.Id,
            SelectedTemplateId = selectedCycle?.TemplateId,
            SelectedCycleName = selectedCycle?.Name,
            Cycles = cycles.Select(x => MapCycle(x, templateById)).ToList(),
            Employees = employees.Select(MapEmployee).ToList(),
            Phases = phases.Select(x => new WorkflowPhaseLookupDto
            {
                PhaseId = x.Id,
                TemplateId = x.TemplateId,
                PhaseCode = x.Code,
                PhaseName = x.Name,
                PhaseOrder = x.PhaseOrder,
                IsTerminal = x.IsTerminal
            }).ToList(),
            Participants = participants
                .Select(x => MapParticipant(x, employeeById, phaseById))
                .OrderBy(x => x.Matricola)
                .ThenBy(x => x.EmployeeName)
                .ToList()
        };
    }

    public async Task<CycleParticipantAdminListItemDto> SaveAsync(Guid? participantId, SaveCycleParticipantInput input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidateInput(input);

        var cycle = await _cycleRepository.GetAsync(input.CycleId);
        await ValidateReferencesAsync(cycle, input);

        CycleParticipant entity;

        if (participantId.HasValue)
        {
            entity = await _participantRepository.GetAsync(participantId.Value);

            if (entity.CycleId != input.CycleId)
            {
                throw new BusinessException("ParticipantCycleMismatch");
            }
        }
        else
        {
            if (await _participantRepository.AnyAsync(x => x.CycleId == input.CycleId && x.EmployeeId == input.EmployeeId))
            {
                throw new BusinessException("CycleParticipantAlreadyExists");
            }

            entity = new CycleParticipant
            {
                TenantId = CurrentTenant.Id,
                CycleId = input.CycleId,
                EmployeeId = input.EmployeeId
            };
        }

        entity.EmployeeId = input.EmployeeId;
        entity.CurrentPhaseId = input.CurrentPhaseId;
        entity.Status = input.Status;

        entity = participantId.HasValue
            ? await _participantRepository.UpdateAsync(entity, autoSave: true)
            : await _participantRepository.InsertAsync(entity, autoSave: true);

        var employee = await _employeeRepository.GetAsync(entity.EmployeeId);
        ProcessPhase? phase = null;
        if (entity.CurrentPhaseId.HasValue)
        {
            phase = await _phaseRepository.FindAsync(entity.CurrentPhaseId.Value);
        }

        return MapParticipant(
            entity,
            new Dictionary<Guid, Employee> { [employee.Id] = employee },
            phase is null ? new Dictionary<Guid, ProcessPhase>() : new Dictionary<Guid, ProcessPhase> { [phase.Id] = phase });
    }

    public async Task DeleteAsync(Guid participantId)
    {
        EnsureTenantAndAdmin();
        await _participantRepository.DeleteAsync(participantId);
    }

    private void EnsureTenantAndAdmin()
    {
        if (CurrentTenant.Id is null)
        {
            throw new BusinessException("TenantMissing");
        }

        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!roles.Any(x => x.Contains("admin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("CurrentUserIsNotAdmin");
        }
    }

    private async Task ValidateReferencesAsync(Cycle cycle, SaveCycleParticipantInput input)
    {
        if (!await _employeeRepository.AnyAsync(x => x.Id == input.EmployeeId && x.IsActive))
        {
            throw new BusinessException("EmployeeNotFoundOrInactive");
        }

        if (input.CurrentPhaseId.HasValue &&
            !await _phaseRepository.AnyAsync(x => x.Id == input.CurrentPhaseId.Value && x.TemplateId == cycle.TemplateId))
        {
            throw new BusinessException("PhaseNotInTemplate");
        }
    }

    private static Cycle? ResolveSelectedCycle(List<Cycle> cycles, Guid? cycleId)
    {
        if (cycles.Count == 0)
        {
            return null;
        }

        if (cycleId.HasValue)
        {
            var selected = cycles.FirstOrDefault(x => x.Id == cycleId.Value);

            if (selected is null)
            {
                throw new BusinessException("CycleNotFound");
            }

            return selected;
        }

        return cycles
            .OrderByDescending(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.CycleYear)
            .ThenBy(x => x.Name)
            .First();
    }

    private static void NormalizeAndValidateInput(SaveCycleParticipantInput input)
    {
        if (input.CycleId == Guid.Empty)
        {
            throw new BusinessException("CycleIdRequired");
        }

        if (input.EmployeeId == Guid.Empty)
        {
            throw new BusinessException("EmployeeIdRequired");
        }

        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "Active" : input.Status.Trim();

        if (input.Status is not ("Active" or "Completed" or "Excluded"))
        {
            throw new BusinessException("CycleParticipantStatusInvalid");
        }
    }

    private static CycleAdminListItemDto MapCycle(Cycle cycle, Dictionary<Guid, ProcessTemplate> templateById)
    {
        templateById.TryGetValue(cycle.TemplateId, out var template);

        return new CycleAdminListItemDto
        {
            CycleId = cycle.Id,
            Name = cycle.Name,
            CycleYear = cycle.CycleYear,
            TemplateId = cycle.TemplateId,
            TemplateName = template?.Name ?? string.Empty,
            CurrentPhaseId = cycle.CurrentPhaseId,
            Status = cycle.Status,
            StartDate = cycle.StartDate,
            EndDate = cycle.EndDate
        };
    }

    private static EmployeeAdminListItemDto MapEmployee(Employee employee)
    {
        return new EmployeeAdminListItemDto
        {
            EmployeeId = employee.Id,
            UserId = employee.UserId,
            Matricola = employee.Matricola,
            FullName = employee.FullName,
            Email = employee.Email,
            OrgUnitId = employee.OrgUnitId,
            JobRoleId = employee.JobRoleId,
            IsActive = employee.IsActive
        };
    }

    private static CycleParticipantAdminListItemDto MapParticipant(
        CycleParticipant participant,
        Dictionary<Guid, Employee> employeeById,
        Dictionary<Guid, ProcessPhase> phaseById)
    {
        employeeById.TryGetValue(participant.EmployeeId, out var employee);

        ProcessPhase? phase = null;
        if (participant.CurrentPhaseId.HasValue)
        {
            phaseById.TryGetValue(participant.CurrentPhaseId.Value, out phase);
        }

        return new CycleParticipantAdminListItemDto
        {
            ParticipantId = participant.Id,
            CycleId = participant.CycleId,
            EmployeeId = participant.EmployeeId,
            Matricola = employee?.Matricola ?? string.Empty,
            EmployeeName = employee?.FullName ?? string.Empty,
            EmployeeEmail = employee?.Email,
            CurrentPhaseId = participant.CurrentPhaseId,
            CurrentPhaseCode = phase?.Code,
            CurrentPhaseName = phase?.Name,
            Status = participant.Status
        };
    }
}

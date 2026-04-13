using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Cycles;
using SuccessFactor.Process;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Admin;

[Authorize]
public class AdminCycleAppService : ApplicationService, IAdminCycleAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<ProcessTemplate, Guid> _templateRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;

    public AdminCycleAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<ProcessTemplate, Guid> templateRepository,
        IRepository<ProcessPhase, Guid> phaseRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _templateRepository = templateRepository;
        _phaseRepository = phaseRepository;
    }

    public async Task<CycleAdminDto> GetAsync(Guid? templateId = null)
    {
        EnsureTenantAndAdmin();

        var templateQuery = await _templateRepository.GetQueryableAsync();
        var templates = await _asyncExecuter.ToListAsync(
            templateQuery
                .OrderBy(x => x.Name)
                .ThenByDescending(x => x.Version));

        var phaseQuery = await _phaseRepository.GetQueryableAsync();
        var phases = await _asyncExecuter.ToListAsync(
            phaseQuery
                .OrderBy(x => x.TemplateId)
                .ThenBy(x => x.PhaseOrder)
                .ThenBy(x => x.Code));

        var cycleQuery = await _cycleRepository.GetQueryableAsync();
        var cycles = await _asyncExecuter.ToListAsync(
            cycleQuery
                .OrderByDescending(x => x.CycleYear)
                .ThenBy(x => x.Name));

        var templateById = templates.ToDictionary(x => x.Id, x => x);
        var phaseById = phases.ToDictionary(x => x.Id, x => x);

        return new CycleAdminDto
        {
            Templates = templates.Select(x => new WorkflowTemplateLookupDto
            {
                TemplateId = x.Id,
                TemplateName = x.Name,
                Version = x.Version,
                IsDefault = x.IsDefault
            }).ToList(),
            Phases = phases.Select(x => new WorkflowPhaseLookupDto
            {
                PhaseId = x.Id,
                TemplateId = x.TemplateId,
                PhaseCode = x.Code,
                PhaseName = x.Name,
                PhaseOrder = x.PhaseOrder,
                IsTerminal = x.IsTerminal
            }).ToList(),
            Cycles = cycles.Select(x => MapCycle(x, templateById, phaseById)).ToList()
        };
    }

    public async Task<CycleAdminListItemDto> SaveAsync(Guid? id, CreateUpdateCycleDto input)
    {
        EnsureTenantAndAdmin();
        NormalizeAndValidateInput(input);
        await ValidateReferencesAsync(input);
        await EnsureNoDuplicateNameAsync(id, input.Name, input.CycleYear);

        Cycle entity;

        if (id.HasValue)
        {
            entity = await _cycleRepository.GetAsync(id.Value);
        }
        else
        {
            entity = new Cycle
            {
                TenantId = CurrentTenant.Id
            };
        }

        entity.Name = input.Name;
        entity.CycleYear = input.CycleYear;
        entity.TemplateId = input.TemplateId;
        entity.CurrentPhaseId = input.CurrentPhaseId;
        entity.Status = input.Status;
        entity.StartDate = input.StartDate;
        entity.EndDate = input.EndDate;

        if (id.HasValue)
        {
            entity = await _cycleRepository.UpdateAsync(entity, autoSave: true);
        }
        else
        {
            entity = await _cycleRepository.InsertAsync(entity, autoSave: true);
        }

        var template = await _templateRepository.GetAsync(entity.TemplateId);
        ProcessPhase? phase = null;

        if (entity.CurrentPhaseId.HasValue)
        {
            phase = await _phaseRepository.FindAsync(entity.CurrentPhaseId.Value);
        }

        var phaseById = phase is null
            ? new Dictionary<Guid, ProcessPhase>()
            : new Dictionary<Guid, ProcessPhase> { [phase.Id] = phase };

        return MapCycle(entity, new Dictionary<Guid, ProcessTemplate> { [template.Id] = template }, phaseById);
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

    private async Task ValidateReferencesAsync(CreateUpdateCycleDto input)
    {
        if (!await _templateRepository.AnyAsync(x => x.Id == input.TemplateId))
        {
            throw new BusinessException("ProcessTemplateNotFound");
        }

        if (input.CurrentPhaseId.HasValue &&
            !await _phaseRepository.AnyAsync(x => x.Id == input.CurrentPhaseId.Value && x.TemplateId == input.TemplateId))
        {
            throw new BusinessException("PhaseNotInTemplate");
        }
    }

    private async Task EnsureNoDuplicateNameAsync(Guid? excludeId, string name, int cycleYear)
    {
        if (await _cycleRepository.AnyAsync(x =>
            x.Name == name &&
            x.CycleYear == cycleYear &&
            (!excludeId.HasValue || x.Id != excludeId.Value)))
        {
            throw new BusinessException("CycleAlreadyExists");
        }
    }

    private static void NormalizeAndValidateInput(CreateUpdateCycleDto input)
    {
        input.Name = NormalizeRequired(input.Name, "Name");
        input.Status = NormalizeRequired(input.Status, "Status");

        if (input.TemplateId == Guid.Empty)
        {
            throw new BusinessException("TemplateIdRequired");
        }

        if (input.CycleYear is < 2000 or > 2100)
        {
            throw new BusinessException("CycleYearInvalid");
        }

        if (input.Status is not ("Draft" or "Active" or "Closed"))
        {
            throw new BusinessException("CycleStatusInvalid");
        }

        if (input.StartDate.HasValue && input.EndDate.HasValue && input.StartDate.Value > input.EndDate.Value)
        {
            throw new BusinessException("StartDateAfterEndDate");
        }
    }

    private static CycleAdminListItemDto MapCycle(
        Cycle cycle,
        Dictionary<Guid, ProcessTemplate> templateById,
        Dictionary<Guid, ProcessPhase> phaseById)
    {
        templateById.TryGetValue(cycle.TemplateId, out var template);

        ProcessPhase? phase = null;
        if (cycle.CurrentPhaseId.HasValue)
        {
            phaseById.TryGetValue(cycle.CurrentPhaseId.Value, out phase);
        }

        return new CycleAdminListItemDto
        {
            CycleId = cycle.Id,
            Name = cycle.Name,
            CycleYear = cycle.CycleYear,
            TemplateId = cycle.TemplateId,
            TemplateName = template?.Name ?? string.Empty,
            CurrentPhaseId = cycle.CurrentPhaseId,
            CurrentPhaseCode = phase?.Code,
            CurrentPhaseName = phase?.Name,
            Status = cycle.Status,
            StartDate = cycle.StartDate,
            EndDate = cycle.EndDate
        };
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException($"{fieldName}Required");
        }

        return value.Trim();
    }
}

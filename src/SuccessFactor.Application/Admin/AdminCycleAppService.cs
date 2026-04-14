using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Cycles;
using SuccessFactor.Goals;
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
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<GoalAssignment, Guid> _goalAssignmentRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<ProcessTemplate, Guid> _templateRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;

    public AdminCycleAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<ProcessTemplate, Guid> templateRepository,
        IRepository<ProcessPhase, Guid> phaseRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _goalAssignmentRepository = goalAssignmentRepository;
        _assessmentRepository = assessmentRepository;
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
        var statsByCycleId = await GetStatsByCycleIdAsync(cycles.Select(x => x.Id).ToList());

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
            Cycles = cycles.Select(x => MapCycle(x, templateById, phaseById, statsByCycleId)).ToList()
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
            await ValidateCycleEditAsync(entity, input);
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
        var statsByCycleId = await GetStatsByCycleIdAsync([entity.Id]);

        return MapCycle(entity, new Dictionary<Guid, ProcessTemplate> { [template.Id] = template }, phaseById, statsByCycleId);
    }

    public async Task ActivateAsync(Guid cycleId)
    {
        EnsureTenantAndAdmin();

        var entity = await _cycleRepository.GetAsync(cycleId);
        await ValidateActivationAsync(entity);

        entity.Status = "Active";
        entity.StartDate ??= DateOnly.FromDateTime(Clock.Now);

        await _cycleRepository.UpdateAsync(entity, autoSave: true);
    }

    public async Task CloseAsync(Guid cycleId)
    {
        EnsureTenantAndAdmin();

        var entity = await _cycleRepository.GetAsync(cycleId);
        await ValidateClosureAsync(entity);

        entity.Status = "Closed";
        entity.EndDate ??= DateOnly.FromDateTime(Clock.Now);

        await _cycleRepository.UpdateAsync(entity, autoSave: true);
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

    private async Task ValidateCycleEditAsync(Cycle entity, CreateUpdateCycleDto input)
    {
        var hasSetupData = await HasSetupDataAsync(entity.Id);

        if (hasSetupData && entity.TemplateId != input.TemplateId)
        {
            throw new BusinessException("CycleTemplateLockedBySetupData");
        }

        if (string.Equals(entity.Status, "Closed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(input.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("ClosedCycleCannotBeReopened");
        }

        if (string.Equals(entity.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(input.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("ActiveCycleCannotReturnToDraft");
        }

        if (!string.Equals(entity.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(input.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            await ValidateActivationAsync(entity, input.CurrentPhaseId);
        }

        if (!string.Equals(entity.Status, "Closed", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(input.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            await ValidateClosureAsync(entity);
        }
    }

    private async Task ValidateActivationAsync(Cycle cycle, Guid? currentPhaseId = null)
    {
        if (string.Equals(cycle.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("ClosedCycleCannotBeActivated");
        }

        if (!(currentPhaseId ?? cycle.CurrentPhaseId).HasValue)
        {
            throw new BusinessException("CurrentPhaseRequiredForActivation");
        }

        if (!await _participantRepository.AnyAsync(x => x.CycleId == cycle.Id))
        {
            throw new BusinessException("CycleRequiresParticipantsForActivation");
        }
    }

    private async Task ValidateClosureAsync(Cycle cycle)
    {
        if (!string.Equals(cycle.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("OnlyActiveCycleCanBeClosed");
        }

        if (await _assessmentRepository.AnyAsync(x => x.CycleId == cycle.Id && x.Status == "Draft"))
        {
            throw new BusinessException("CycleHasDraftAssessments");
        }
    }

    private async Task<bool> HasSetupDataAsync(Guid cycleId)
    {
        return await _participantRepository.AnyAsync(x => x.CycleId == cycleId) ||
               await _goalAssignmentRepository.AnyAsync(x => x.CycleId == cycleId) ||
               await _assessmentRepository.AnyAsync(x => x.CycleId == cycleId);
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
        Dictionary<Guid, ProcessPhase> phaseById,
        Dictionary<Guid, CycleAdminStats> statsByCycleId)
    {
        templateById.TryGetValue(cycle.TemplateId, out var template);
        statsByCycleId.TryGetValue(cycle.Id, out var stats);
        stats ??= new CycleAdminStats();

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
            EndDate = cycle.EndDate,
            ParticipantCount = stats.ParticipantCount,
            GoalAssignmentCount = stats.GoalAssignmentCount,
            AssessmentCount = stats.AssessmentCount,
            DraftAssessmentCount = stats.DraftAssessmentCount,
            HasSetupData = stats.HasSetupData,
            CanActivate = cycle.Status == "Draft" && cycle.CurrentPhaseId.HasValue && stats.ParticipantCount > 0,
            CanClose = cycle.Status == "Active" && stats.DraftAssessmentCount == 0
        };
    }

    private async Task<Dictionary<Guid, CycleAdminStats>> GetStatsByCycleIdAsync(List<Guid> cycleIds)
    {
        var result = cycleIds.ToDictionary(x => x, _ => new CycleAdminStats());

        if (cycleIds.Count == 0)
        {
            return result;
        }

        var participants = await _participantRepository.GetListAsync(x => cycleIds.Contains(x.CycleId));
        foreach (var group in participants.GroupBy(x => x.CycleId))
        {
            result[group.Key].ParticipantCount = group.Count();
        }

        var goalAssignments = await _goalAssignmentRepository.GetListAsync(x => cycleIds.Contains(x.CycleId));
        foreach (var group in goalAssignments.GroupBy(x => x.CycleId))
        {
            result[group.Key].GoalAssignmentCount = group.Count();
        }

        var assessments = await _assessmentRepository.GetListAsync(x => cycleIds.Contains(x.CycleId));
        foreach (var group in assessments.GroupBy(x => x.CycleId))
        {
            result[group.Key].AssessmentCount = group.Count();
            result[group.Key].DraftAssessmentCount = group.Count(x => x.Status == "Draft");
        }

        return result;
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException($"{fieldName}Required");
        }

        return value.Trim();
    }

    private class CycleAdminStats
    {
        public int ParticipantCount { get; set; }
        public int GoalAssignmentCount { get; set; }
        public int AssessmentCount { get; set; }
        public int DraftAssessmentCount { get; set; }
        public bool HasSetupData => ParticipantCount > 0 || GoalAssignmentCount > 0 || AssessmentCount > 0;
    }
}

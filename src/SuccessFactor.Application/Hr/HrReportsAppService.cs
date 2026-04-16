using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Hr;

[Authorize]
public class HrReportsAppService : ApplicationService, IHrReportsAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<EmployeeManager, Guid> _managerRelationRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;
    private readonly IRepository<CompetencyModel, Guid> _modelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;

    public HrReportsAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<EmployeeManager, Guid> managerRelationRepository,
        IRepository<ProcessPhase, Guid> phaseRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository,
        IRepository<CompetencyModel, Guid> modelRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _employeeRepository = employeeRepository;
        _managerRelationRepository = managerRelationRepository;
        _phaseRepository = phaseRepository;
        _assessmentRepository = assessmentRepository;
        _assessmentItemRepository = assessmentItemRepository;
        _modelRepository = modelRepository;
        _modelItemRepository = modelItemRepository;
    }

    public async Task<HrReportDto> GetAsync(GetHrReportInput input)
    {
        EnsureTenantAndHrOrAdmin();
        input ??= new GetHrReportInput();

        var cycleQuery = await _cycleRepository.GetQueryableAsync();
        var cycles = await _asyncExecuter.ToListAsync(
            cycleQuery
                .OrderByDescending(x => x.CycleYear)
                .ThenByDescending(x => x.CreationTime)
                .ThenBy(x => x.Name));

        var selectedCycle = ResolveSelectedCycle(cycles, input.CycleId);
        var dto = new HrReportDto
        {
            SelectedCycleId = selectedCycle?.Id,
            SelectedCycleName = selectedCycle?.Name,
            SelectedCycleStatus = selectedCycle?.Status,
            Cycles = cycles.Select(x => new HrCycleLookupDto
            {
                CycleId = x.Id,
                CycleName = x.Name,
                CycleStatus = x.Status,
                IsSelected = selectedCycle is not null && x.Id == selectedCycle.Id
            }).ToList()
        };

        if (selectedCycle is null)
        {
            return dto;
        }

        var participants = await _asyncExecuter.ToListAsync(
            (await _participantRepository.GetQueryableAsync())
                .Where(x => x.CycleId == selectedCycle.Id));

        var participantEmployeeIds = participants
            .Where(x => !string.Equals(x.Status, "Excluded", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToList();

        var assessments = await _asyncExecuter.ToListAsync(
            (await _assessmentRepository.GetQueryableAsync())
                .Where(x => x.CycleId == selectedCycle.Id));

        var allEmployeeIds = participantEmployeeIds
            .Concat(assessments.Select(x => x.EmployeeId))
            .Concat(assessments.Select(x => x.EvaluatorEmployeeId))
            .Distinct()
            .ToList();

        var employees = allEmployeeIds.Count == 0
            ? new List<Employee>()
            : await _asyncExecuter.ToListAsync(
                (await _employeeRepository.GetQueryableAsync())
                    .Where(x => allEmployeeIds.Contains(x.Id)));
        var employeeById = employees.ToDictionary(x => x.Id, x => x);

        var phases = await LoadPhasesAsync(participants);
        var phaseById = phases.ToDictionary(x => x.Id, x => x);

        var assessmentItems = await LoadAssessmentItemsAsync(assessments);
        var models = await LoadModelsAsync(assessments);
        var modelById = models.ToDictionary(x => x.Id, x => x);
        var modelItems = await LoadModelItemsAsync(models);

        dto.TotalParticipants = participants.Count;
        dto.ActiveParticipants = participants.Count(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase));
        dto.CompletedParticipants = participants.Count(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        dto.ExcludedParticipants = participants.Count(x => string.Equals(x.Status, "Excluded", StringComparison.OrdinalIgnoreCase));
        dto.TotalAssessments = assessments.Count;
        dto.DraftAssessments = assessments.Count(x => string.Equals(x.Status, "Draft", StringComparison.OrdinalIgnoreCase));
        dto.SubmittedAssessments = assessments.Count(x => string.Equals(x.Status, "Submitted", StringComparison.OrdinalIgnoreCase));
        dto.ClosedAssessments = assessments.Count(x => string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase));

        dto.ParticipantStatuses = BuildParticipantStatuses(participants);
        dto.Phases = BuildPhaseRows(participants, phaseById);
        dto.AssessmentStatuses = BuildAssessmentGroups(assessments, x => x.Status);
        dto.AssessmentTypes = BuildAssessmentGroups(assessments, x => x.AssessmentType);
        dto.MissingRequiredAssessments = BuildMissingRequiredRows(assessments, assessmentItems, modelItems, employeeById, modelById);
        dto.AssessmentsWithMissingRequired = dto.MissingRequiredAssessments.Count;
        dto.EmployeesWithoutManager = await BuildEmployeesWithoutManagerAsync(participantEmployeeIds, employeeById);
        dto.EmployeesWithoutUserLink = BuildEmployeesWithoutUserLink(participantEmployeeIds, employeeById);
        dto.EmployeesWithoutManagerCount = dto.EmployeesWithoutManager.Count;
        dto.EmployeesWithoutUserLinkCount = dto.EmployeesWithoutUserLink.Count;

        return dto;
    }

    private async Task<List<ProcessPhase>> LoadPhasesAsync(List<CycleParticipant> participants)
    {
        var phaseIds = participants
            .Where(x => x.CurrentPhaseId.HasValue)
            .Select(x => x.CurrentPhaseId!.Value)
            .Distinct()
            .ToList();

        if (phaseIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _phaseRepository.GetQueryableAsync())
                .Where(x => phaseIds.Contains(x.Id)));
    }

    private async Task<List<CompetencyAssessmentItem>> LoadAssessmentItemsAsync(List<CompetencyAssessment> assessments)
    {
        var assessmentIds = assessments.Select(x => x.Id).Distinct().ToList();
        if (assessmentIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _assessmentItemRepository.GetQueryableAsync())
                .Where(x => assessmentIds.Contains(x.AssessmentId)));
    }

    private async Task<List<CompetencyModel>> LoadModelsAsync(List<CompetencyAssessment> assessments)
    {
        var modelIds = assessments
            .Where(x => x.ModelId.HasValue)
            .Select(x => x.ModelId!.Value)
            .Distinct()
            .ToList();

        if (modelIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _modelRepository.GetQueryableAsync())
                .Where(x => modelIds.Contains(x.Id)));
    }

    private async Task<List<CompetencyModelItem>> LoadModelItemsAsync(List<CompetencyModel> models)
    {
        var modelIds = models.Select(x => x.Id).Distinct().ToList();
        if (modelIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _modelItemRepository.GetQueryableAsync())
                .Where(x => modelIds.Contains(x.ModelId)));
    }

    private async Task<List<HrReportEmployeeIssueDto>> BuildEmployeesWithoutManagerAsync(
        List<Guid> participantEmployeeIds,
        Dictionary<Guid, Employee> employeeById)
    {
        if (participantEmployeeIds.Count == 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(Clock.Now);
        var relations = await _asyncExecuter.ToListAsync(
            (await _managerRelationRepository.GetQueryableAsync())
                .Where(x => participantEmployeeIds.Contains(x.EmployeeId)));

        var employeeIdsWithActiveManager = relations
            .Where(x => (!x.StartDate.HasValue || x.StartDate.Value <= today) &&
                        (!x.EndDate.HasValue || x.EndDate.Value >= today))
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToHashSet();

        return participantEmployeeIds
            .Where(x => employeeById.TryGetValue(x, out var employee) &&
                        employee.IsActive &&
                        !employeeIdsWithActiveManager.Contains(x))
            .Select(x => MapEmployeeIssue(employeeById[x], "Nessuna relazione manager attiva nel ciclo selezionato."))
            .OrderBy(x => x.Matricola)
            .ThenBy(x => x.FullName)
            .ToList();
    }

    private static List<HrReportEmployeeIssueDto> BuildEmployeesWithoutUserLink(
        List<Guid> participantEmployeeIds,
        Dictionary<Guid, Employee> employeeById)
    {
        return participantEmployeeIds
            .Where(x => employeeById.TryGetValue(x, out var employee) &&
                        employee.IsActive &&
                        !employee.UserId.HasValue)
            .Select(x => MapEmployeeIssue(employeeById[x], "Employee attivo senza collegamento a utente ABP."))
            .OrderBy(x => x.Matricola)
            .ThenBy(x => x.FullName)
            .ToList();
    }

    private static List<HrReportParticipantStatusDto> BuildParticipantStatuses(List<CycleParticipant> participants)
    {
        return participants
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Status) ? "-" : x.Status)
            .Select(x => new HrReportParticipantStatusDto
            {
                Status = x.Key,
                Count = x.Count()
            })
            .OrderBy(x => x.Status)
            .ToList();
    }

    private static List<HrReportPhaseDto> BuildPhaseRows(
        List<CycleParticipant> participants,
        Dictionary<Guid, ProcessPhase> phaseById)
    {
        return participants
            .GroupBy(x => x.CurrentPhaseId)
            .Select(x =>
            {
                ProcessPhase? phase = null;
                if (x.Key.HasValue)
                {
                    phaseById.TryGetValue(x.Key.Value, out phase);
                }

                return new HrReportPhaseDto
                {
                    PhaseId = x.Key,
                    PhaseCode = phase?.Code ?? "-",
                    PhaseName = phase?.Name ?? "Nessuna fase",
                    Count = x.Count()
                };
            })
            .OrderBy(x => x.PhaseCode)
            .ToList();
    }

    private static List<HrReportAssessmentGroupDto> BuildAssessmentGroups(
        List<CompetencyAssessment> assessments,
        Func<CompetencyAssessment, string?> keySelector)
    {
        return assessments
            .GroupBy(x => string.IsNullOrWhiteSpace(keySelector(x)) ? "-" : keySelector(x)!)
            .Select(x => new HrReportAssessmentGroupDto
            {
                Key = x.Key,
                Label = x.Key,
                Count = x.Count()
            })
            .OrderBy(x => x.Label)
            .ToList();
    }

    private static List<HrReportMissingRequiredAssessmentDto> BuildMissingRequiredRows(
        List<CompetencyAssessment> assessments,
        List<CompetencyAssessmentItem> assessmentItems,
        List<CompetencyModelItem> modelItems,
        Dictionary<Guid, Employee> employeeById,
        Dictionary<Guid, CompetencyModel> modelById)
    {
        var itemsByAssessmentId = assessmentItems
            .GroupBy(x => x.AssessmentId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var requiredModelItemsByModelId = modelItems
            .Where(x => x.IsRequired)
            .GroupBy(x => x.ModelId)
            .ToDictionary(x => x.Key, x => x.ToList());

        return assessments
            .Where(x => x.ModelId.HasValue)
            .Select(x =>
            {
                employeeById.TryGetValue(x.EmployeeId, out var employee);
                employeeById.TryGetValue(x.EvaluatorEmployeeId, out var evaluator);
                modelById.TryGetValue(x.ModelId!.Value, out var model);
                requiredModelItemsByModelId.TryGetValue(x.ModelId.Value, out var requiredModelItems);
                itemsByAssessmentId.TryGetValue(x.Id, out var currentItems);

                requiredModelItems ??= [];
                currentItems ??= [];
                var itemByCompetencyId = currentItems
                    .GroupBy(i => i.CompetencyId)
                    .ToDictionary(i => i.Key, i => i.First());
                var missingRequiredCount = requiredModelItems.Count(i =>
                    !itemByCompetencyId.TryGetValue(i.CompetencyId, out var item) ||
                    !item.Score.HasValue);

                return new HrReportMissingRequiredAssessmentDto
                {
                    AssessmentId = x.Id,
                    EmployeeId = x.EmployeeId,
                    Matricola = employee?.Matricola ?? string.Empty,
                    EmployeeName = employee?.FullName ?? string.Empty,
                    EvaluatorName = evaluator?.FullName ?? string.Empty,
                    AssessmentType = x.AssessmentType,
                    Status = x.Status,
                    ModelName = model?.Name ?? string.Empty,
                    RequiredItemsCount = requiredModelItems.Count,
                    MissingRequiredCount = missingRequiredCount
                };
            })
            .Where(x => x.MissingRequiredCount > 0)
            .OrderBy(x => x.Matricola)
            .ThenBy(x => x.EmployeeName)
            .ThenBy(x => x.AssessmentType)
            .ToList();
    }

    private static HrReportEmployeeIssueDto MapEmployeeIssue(Employee employee, string issue)
    {
        return new HrReportEmployeeIssueDto
        {
            EmployeeId = employee.Id,
            Matricola = employee.Matricola,
            FullName = employee.FullName,
            Email = employee.Email,
            Issue = issue
        };
    }

    private void EnsureTenantAndHrOrAdmin()
    {
        if (CurrentTenant.Id is null)
        {
            throw new BusinessException("TenantMissing");
        }

        var roles = _currentUser.Roles ?? Array.Empty<string>();
        var isAllowed = roles.Any(x =>
            x.Contains("hr", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("admin", StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            throw new BusinessException("CurrentUserIsNotHr");
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
                throw new BusinessException("HrCycleNotFound");
            }

            return selected;
        }

        return cycles
            .OrderByDescending(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.CycleYear)
            .ThenByDescending(x => x.CreationTime)
            .First();
    }
}

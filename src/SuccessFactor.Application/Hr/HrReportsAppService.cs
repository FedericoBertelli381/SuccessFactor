using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Goals;
using SuccessFactor.JobRoles;
using SuccessFactor.OrgUnits;
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
    private readonly IRepository<OrgUnit, Guid> _orgUnitRepository;
    private readonly IRepository<JobRole, Guid> _jobRoleRepository;
    private readonly IRepository<Goal, Guid> _goalRepository;
    private readonly IRepository<GoalAssignment, Guid> _goalAssignmentRepository;
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
        IRepository<OrgUnit, Guid> orgUnitRepository,
        IRepository<JobRole, Guid> jobRoleRepository,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository,
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
        _orgUnitRepository = orgUnitRepository;
        _jobRoleRepository = jobRoleRepository;
        _goalRepository = goalRepository;
        _goalAssignmentRepository = goalAssignmentRepository;
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
            }).ToList(),
            ExportOrgUnits = await LoadOrgUnitLookupsAsync(),
            ExportJobRoles = await LoadJobRoleLookupsAsync()
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

    public async Task<HrExportFileDto> ExportCsvAsync(GetHrExportInput input)
    {
        EnsureTenantAndHrOrAdmin();
        input ??= new GetHrExportInput();

        var context = await BuildExportContextAsync(input);
        var csv = input.ExportKind switch
        {
            HrExportKind.Employees => BuildEmployeesCsv(context),
            HrExportKind.Participants => BuildParticipantsCsv(context),
            HrExportKind.Goals => BuildGoalsCsv(context),
            HrExportKind.Assessments => BuildAssessmentsCsv(context),
            HrExportKind.HrReport => BuildHrReportCsv(context),
            _ => throw new BusinessException("HrExportKindNotSupported")
        };

        return new HrExportFileDto
        {
            FileName = BuildExportFileName(input.ExportKind, context.SelectedCycle?.Name),
            ContentType = "text/csv; charset=utf-8",
            Content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray()
        };
    }

    private async Task<HrExportContext> BuildExportContextAsync(GetHrExportInput input)
    {
        var cycles = await _asyncExecuter.ToListAsync(
            (await _cycleRepository.GetQueryableAsync())
                .OrderByDescending(x => x.CycleYear)
                .ThenByDescending(x => x.CreationTime)
                .ThenBy(x => x.Name));
        var selectedCycle = ResolveSelectedCycle(cycles, input.CycleId);

        var participants = selectedCycle is null
            ? new List<CycleParticipant>()
            : await _asyncExecuter.ToListAsync(
                (await _participantRepository.GetQueryableAsync())
                    .Where(x => x.CycleId == selectedCycle.Id));

        if (input.PhaseId.HasValue)
        {
            participants = participants.Where(x => x.CurrentPhaseId == input.PhaseId.Value).ToList();
        }

        var participantEmployeeIds = participants.Select(x => x.EmployeeId).Distinct().ToList();
        var assignments = input.PhaseId.HasValue && participantEmployeeIds.Count == 0
            ? new List<GoalAssignment>()
            : await LoadGoalAssignmentsForExportAsync(selectedCycle?.Id, participantEmployeeIds);
        var assessments = input.PhaseId.HasValue && participantEmployeeIds.Count == 0
            ? new List<CompetencyAssessment>()
            : await LoadAssessmentsForExportAsync(selectedCycle?.Id, participantEmployeeIds);

        var employeeIds = participantEmployeeIds
            .Concat(assignments.Select(x => x.EmployeeId))
            .Concat(assessments.Select(x => x.EmployeeId))
            .Concat(assessments.Select(x => x.EvaluatorEmployeeId))
            .Distinct()
            .ToList();

        if (employeeIds.Count == 0 && input.ExportKind == HrExportKind.Employees && selectedCycle is null && !input.PhaseId.HasValue)
        {
            employeeIds = await _asyncExecuter.ToListAsync(
                (await _employeeRepository.GetQueryableAsync()).Select(x => x.Id));
        }

        var employees = employeeIds.Count == 0
            ? new List<Employee>()
            : await _asyncExecuter.ToListAsync(
                (await _employeeRepository.GetQueryableAsync())
                    .Where(x => employeeIds.Contains(x.Id)));

        employees = employees
            .Where(x => (!input.OrgUnitId.HasValue || x.OrgUnitId == input.OrgUnitId.Value) &&
                        (!input.JobRoleId.HasValue || x.JobRoleId == input.JobRoleId.Value))
            .ToList();
        var allowedEmployeeIds = employees.Select(x => x.Id).ToHashSet();

        participants = participants.Where(x => allowedEmployeeIds.Contains(x.EmployeeId)).ToList();
        assignments = assignments.Where(x => allowedEmployeeIds.Contains(x.EmployeeId)).ToList();
        assessments = assessments.Where(x => allowedEmployeeIds.Contains(x.EmployeeId)).ToList();

        var phases = await LoadPhasesAsync(participants);
        var orgUnits = await _asyncExecuter.ToListAsync(await _orgUnitRepository.GetQueryableAsync());
        var jobRoles = await _asyncExecuter.ToListAsync(await _jobRoleRepository.GetQueryableAsync());
        var goals = await LoadGoalsForExportAsync(assignments);
        var models = await LoadModelsAsync(assessments);
        var assessmentItems = await LoadAssessmentItemsAsync(assessments);

        return new HrExportContext(
            selectedCycle,
            employees,
            participants,
            phases,
            orgUnits,
            jobRoles,
            assignments,
            goals,
            assessments,
            assessmentItems,
            models);
    }

    private async Task<List<HrExportLookupDto>> LoadOrgUnitLookupsAsync()
    {
        return (await _asyncExecuter.ToListAsync(
                (await _orgUnitRepository.GetQueryableAsync())
                    .OrderBy(x => x.Name)))
            .Select(x => new HrExportLookupDto { Id = x.Id, Name = x.Name })
            .ToList();
    }

    private async Task<List<HrExportLookupDto>> LoadJobRoleLookupsAsync()
    {
        return (await _asyncExecuter.ToListAsync(
                (await _jobRoleRepository.GetQueryableAsync())
                    .OrderBy(x => x.Name)))
            .Select(x => new HrExportLookupDto { Id = x.Id, Name = x.Name })
            .ToList();
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

    private async Task<List<GoalAssignment>> LoadGoalAssignmentsForExportAsync(Guid? cycleId, List<Guid> participantEmployeeIds)
    {
        if (!cycleId.HasValue)
        {
            return [];
        }

        var query = (await _goalAssignmentRepository.GetQueryableAsync())
            .Where(x => x.CycleId == cycleId.Value);

        if (participantEmployeeIds.Count > 0)
        {
            query = query.Where(x => participantEmployeeIds.Contains(x.EmployeeId));
        }

        return await _asyncExecuter.ToListAsync(query);
    }

    private async Task<List<Goal>> LoadGoalsForExportAsync(List<GoalAssignment> assignments)
    {
        var goalIds = assignments.Select(x => x.GoalId).Distinct().ToList();
        if (goalIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _goalRepository.GetQueryableAsync())
                .Where(x => goalIds.Contains(x.Id)));
    }

    private async Task<List<CompetencyAssessment>> LoadAssessmentsForExportAsync(Guid? cycleId, List<Guid> participantEmployeeIds)
    {
        if (!cycleId.HasValue)
        {
            return [];
        }

        var query = (await _assessmentRepository.GetQueryableAsync())
            .Where(x => x.CycleId == cycleId.Value);

        if (participantEmployeeIds.Count > 0)
        {
            query = query.Where(x => participantEmployeeIds.Contains(x.EmployeeId));
        }

        return await _asyncExecuter.ToListAsync(query);
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

    private static string BuildEmployeesCsv(HrExportContext context)
    {
        var orgUnitById = context.OrgUnits.ToDictionary(x => x.Id, x => x.Name);
        var jobRoleById = context.JobRoles.ToDictionary(x => x.Id, x => x.Name);
        var rows = context.Employees
            .OrderBy(x => x.Matricola)
            .ThenBy(x => x.FullName)
            .Select(x => new[]
            {
                x.Id.ToString(),
                x.Matricola,
                x.FullName,
                x.Email ?? string.Empty,
                ResolveName(orgUnitById, x.OrgUnitId),
                ResolveName(jobRoleById, x.JobRoleId),
                x.IsActive ? "true" : "false",
                x.UserId?.ToString() ?? string.Empty
            });

        return BuildCsv(
            ["EmployeeId", "Matricola", "FullName", "Email", "OrgUnit", "JobRole", "IsActive", "UserId"],
            rows);
    }

    private static string BuildParticipantsCsv(HrExportContext context)
    {
        var employeeById = context.Employees.ToDictionary(x => x.Id);
        var phaseById = context.Phases.ToDictionary(x => x.Id);
        var orgUnitById = context.OrgUnits.ToDictionary(x => x.Id, x => x.Name);
        var jobRoleById = context.JobRoles.ToDictionary(x => x.Id, x => x.Name);
        var rows = context.Participants
            .OrderBy(x => GetEmployee(employeeById, x.EmployeeId)?.Matricola)
            .ThenBy(x => GetEmployee(employeeById, x.EmployeeId)?.FullName)
            .Select(x =>
            {
                var employee = GetEmployee(employeeById, x.EmployeeId);
                var phase = x.CurrentPhaseId.HasValue && phaseById.TryGetValue(x.CurrentPhaseId.Value, out var currentPhase)
                    ? currentPhase
                    : null;

                return new[]
                {
                    x.Id.ToString(),
                    context.SelectedCycle?.Name ?? string.Empty,
                    employee?.Matricola ?? string.Empty,
                    employee?.FullName ?? string.Empty,
                    employee?.Email ?? string.Empty,
                    ResolveName(orgUnitById, employee?.OrgUnitId),
                    ResolveName(jobRoleById, employee?.JobRoleId),
                    phase?.Code ?? string.Empty,
                    phase?.Name ?? string.Empty,
                    x.Status,
                    FormatDateTime(x.CreationTime)
                };
            });

        return BuildCsv(
            ["ParticipantId", "Cycle", "Matricola", "FullName", "Email", "OrgUnit", "JobRole", "PhaseCode", "PhaseName", "Status", "CreatedAt"],
            rows);
    }

    private static string BuildGoalsCsv(HrExportContext context)
    {
        var employeeById = context.Employees.ToDictionary(x => x.Id);
        var goalById = context.Goals.ToDictionary(x => x.Id);
        var orgUnitById = context.OrgUnits.ToDictionary(x => x.Id, x => x.Name);
        var jobRoleById = context.JobRoles.ToDictionary(x => x.Id, x => x.Name);
        var rows = context.GoalAssignments
            .OrderBy(x => GetEmployee(employeeById, x.EmployeeId)?.Matricola)
            .ThenBy(x => GetGoal(goalById, x.GoalId)?.Title)
            .Select(x =>
            {
                var employee = GetEmployee(employeeById, x.EmployeeId);
                var goal = GetGoal(goalById, x.GoalId);

                return new[]
                {
                    x.Id.ToString(),
                    context.SelectedCycle?.Name ?? string.Empty,
                    employee?.Matricola ?? string.Empty,
                    employee?.FullName ?? string.Empty,
                    ResolveName(orgUnitById, employee?.OrgUnitId),
                    ResolveName(jobRoleById, employee?.JobRoleId),
                    goal?.Title ?? string.Empty,
                    goal?.Category ?? string.Empty,
                    FormatDecimal(x.Weight),
                    FormatDecimal(x.TargetValue),
                    FormatDate(x.StartDate),
                    FormatDate(x.DueDate),
                    x.Status
                };
            });

        return BuildCsv(
            ["AssignmentId", "Cycle", "Matricola", "FullName", "OrgUnit", "JobRole", "Goal", "Category", "Weight", "TargetValue", "StartDate", "DueDate", "Status"],
            rows);
    }

    private static string BuildAssessmentsCsv(HrExportContext context)
    {
        var employeeById = context.Employees.ToDictionary(x => x.Id);
        var modelById = context.Models.ToDictionary(x => x.Id);
        var orgUnitById = context.OrgUnits.ToDictionary(x => x.Id, x => x.Name);
        var jobRoleById = context.JobRoles.ToDictionary(x => x.Id, x => x.Name);
        var scoreByAssessmentId = context.AssessmentItems
            .Where(x => x.Score.HasValue)
            .GroupBy(x => x.AssessmentId)
            .ToDictionary(x => x.Key, x => x.Average(i => (decimal)i.Score!.Value));
        var rows = context.Assessments
            .OrderBy(x => GetEmployee(employeeById, x.EmployeeId)?.Matricola)
            .ThenBy(x => x.AssessmentType)
            .Select(x =>
            {
                var employee = GetEmployee(employeeById, x.EmployeeId);
                var evaluator = GetEmployee(employeeById, x.EvaluatorEmployeeId);
                var model = x.ModelId.HasValue && modelById.TryGetValue(x.ModelId.Value, out var currentModel)
                    ? currentModel
                    : null;

                return new[]
                {
                    x.Id.ToString(),
                    context.SelectedCycle?.Name ?? string.Empty,
                    employee?.Matricola ?? string.Empty,
                    employee?.FullName ?? string.Empty,
                    ResolveName(orgUnitById, employee?.OrgUnitId),
                    ResolveName(jobRoleById, employee?.JobRoleId),
                    evaluator?.FullName ?? string.Empty,
                    x.AssessmentType,
                    model?.Name ?? string.Empty,
                    x.Status,
                    scoreByAssessmentId.TryGetValue(x.Id, out var score) ? FormatDecimal(score) : string.Empty,
                    FormatDateTime(x.CreationTime),
                    FormatDateTime(x.LastModificationTime)
                };
            });

        return BuildCsv(
            ["AssessmentId", "Cycle", "Matricola", "FullName", "OrgUnit", "JobRole", "Evaluator", "AssessmentType", "Model", "Status", "AverageScore", "CreatedAt", "ModifiedAt"],
            rows);
    }

    private static string BuildHrReportCsv(HrExportContext context)
    {
        var participantCount = context.Participants.Count;
        var activeParticipantCount = context.Participants.Count(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase));
        var completedParticipantCount = context.Participants.Count(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        var excludedParticipantCount = context.Participants.Count(x => string.Equals(x.Status, "Excluded", StringComparison.OrdinalIgnoreCase));
        var assessmentCount = context.Assessments.Count;
        var draftAssessmentCount = context.Assessments.Count(x => string.Equals(x.Status, "Draft", StringComparison.OrdinalIgnoreCase));
        var submittedAssessmentCount = context.Assessments.Count(x => string.Equals(x.Status, "Submitted", StringComparison.OrdinalIgnoreCase));
        var closedAssessmentCount = context.Assessments.Count(x => string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase));
        var scoredItems = context.AssessmentItems.Where(x => x.Score.HasValue).ToList();
        decimal? averageScore = scoredItems.Count == 0 ? null : scoredItems.Average(x => (decimal)x.Score!.Value);

        return BuildCsv(
            ["Metric", "Value"],
            new[]
            {
                new[] { "Cycle", context.SelectedCycle?.Name ?? string.Empty },
                new[] { "CycleStatus", context.SelectedCycle?.Status ?? string.Empty },
                new[] { "Participants", participantCount.ToString() },
                new[] { "ActiveParticipants", activeParticipantCount.ToString() },
                new[] { "CompletedParticipants", completedParticipantCount.ToString() },
                new[] { "ExcludedParticipants", excludedParticipantCount.ToString() },
                new[] { "Assessments", assessmentCount.ToString() },
                new[] { "DraftAssessments", draftAssessmentCount.ToString() },
                new[] { "SubmittedAssessments", submittedAssessmentCount.ToString() },
                new[] { "ClosedAssessments", closedAssessmentCount.ToString() },
                new[] { "AverageScore", FormatDecimal(averageScore) }
            });
    }

    private static string BuildCsv(string[] headers, IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        AppendCsvRow(builder, headers);

        foreach (var row in rows)
        {
            AppendCsvRow(builder, row);
        }

        return builder.ToString();
    }

    private static void AppendCsvRow(StringBuilder builder, IEnumerable<string> values)
    {
        builder.AppendLine(string.Join(";", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        var mustQuote = value.Contains(';') || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        value = value.Replace("\"", "\"\"");
        return mustQuote ? $"\"{value}\"" : value;
    }

    private static string BuildExportFileName(HrExportKind kind, string? cycleName)
    {
        var cycle = SanitizeFileName(string.IsNullOrWhiteSpace(cycleName) ? "all" : cycleName);
        return $"successfactor-{kind.ToString().ToLowerInvariant()}-{cycle}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = System.IO.Path.GetInvalidFileNameChars().ToHashSet();
        var chars = value.Select(x => invalidChars.Contains(x) || char.IsWhiteSpace(x) ? '-' : char.ToLowerInvariant(x)).ToArray();
        return new string(chars).Trim('-');
    }

    private static Employee? GetEmployee(Dictionary<Guid, Employee> employeeById, Guid employeeId)
        => employeeById.TryGetValue(employeeId, out var employee) ? employee : null;

    private static Goal? GetGoal(Dictionary<Guid, Goal> goalById, Guid goalId)
        => goalById.TryGetValue(goalId, out var goal) ? goal : null;

    private static string ResolveName(Dictionary<Guid, string> namesById, Guid? id)
        => id.HasValue && namesById.TryGetValue(id.Value, out var name) ? name : string.Empty;

    private static string FormatDecimal(decimal? value)
        => value.HasValue ? value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

    private static string FormatDate(DateOnly? value)
        => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatDateTime(DateTime? value)
        => value?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

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

    private sealed record HrExportContext(
        Cycle? SelectedCycle,
        List<Employee> Employees,
        List<CycleParticipant> Participants,
        List<ProcessPhase> Phases,
        List<OrgUnit> OrgUnits,
        List<JobRole> JobRoles,
        List<GoalAssignment> GoalAssignments,
        List<Goal> Goals,
        List<CompetencyAssessment> Assessments,
        List<CompetencyAssessmentItem> AssessmentItems,
        List<CompetencyModel> Models);
}

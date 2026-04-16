using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Competencies.Models;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Goals;
using SuccessFactor.Team.Support;
using SuccessFactor.Workflow;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Team;

[Authorize]
public class TeamReportsAppService : ApplicationService, ITeamReportsAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IManagerScopeResolver _managerScopeResolver;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<ProcessPhase, Guid> _phaseRepository;
    private readonly IRepository<GoalAssignment, Guid> _goalAssignmentRepository;
    private readonly IRepository<Goal, Guid> _goalRepository;
    private readonly IRepository<GoalProgressEntry, Guid> _goalProgressEntryRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;
    private readonly IRepository<CompetencyModel, Guid> _modelRepository;
    private readonly IRepository<CompetencyModelItem, Guid> _modelItemRepository;

    public TeamReportsAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IManagerScopeResolver managerScopeResolver,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<ProcessPhase, Guid> phaseRepository,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalProgressEntry, Guid> goalProgressEntryRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository,
        IRepository<CompetencyModel, Guid> modelRepository,
        IRepository<CompetencyModelItem, Guid> modelItemRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _managerScopeResolver = managerScopeResolver;
        _employeeRepository = employeeRepository;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _phaseRepository = phaseRepository;
        _goalAssignmentRepository = goalAssignmentRepository;
        _goalRepository = goalRepository;
        _goalProgressEntryRepository = goalProgressEntryRepository;
        _assessmentRepository = assessmentRepository;
        _assessmentItemRepository = assessmentItemRepository;
        _modelRepository = modelRepository;
        _modelItemRepository = modelItemRepository;
    }

    public async Task<TeamReportDto> GetAsync(GetTeamReportInput input)
    {
        input ??= new GetTeamReportInput();

        var manager = await ResolveActorEmployeeAsync();
        var managedEmployeeIds = await _managerScopeResolver.GetManagedEmployeeIdsAsync(manager.Id);
        var managedEmployees = await LoadManagedEmployeesAsync(managedEmployeeIds);
        var cycles = await LoadCyclesAsync();
        var selectedCycle = ResolveSelectedCycle(cycles, input.CycleId);

        var dto = new TeamReportDto
        {
            ManagerEmployeeId = manager.Id,
            ManagerEmployeeName = manager.FullName,
            TeamMemberCount = managedEmployees.Count,
            SelectedCycleId = selectedCycle?.Id,
            SelectedCycleName = selectedCycle?.Name,
            SelectedCycleStatus = selectedCycle?.Status,
            Cycles = cycles.Select(x => new TeamReportCycleLookupDto
            {
                CycleId = x.Id,
                CycleName = x.Name,
                CycleStatus = x.Status,
                IsSelected = selectedCycle is not null && x.Id == selectedCycle.Id
            }).ToList()
        };

        if (selectedCycle is null || managedEmployeeIds.Count == 0)
        {
            return dto;
        }

        var participants = await LoadParticipantsAsync(selectedCycle.Id, managedEmployeeIds);
        var participantEmployeeIds = participants.Select(x => x.EmployeeId).Distinct().ToList();
        var phases = await LoadPhasesAsync(participants);
        var phaseById = phases.ToDictionary(x => x.Id, x => x);
        var employeeById = managedEmployees.ToDictionary(x => x.Id, x => x);

        var goalRows = await LoadGoalRowsAsync(selectedCycle.Id, participantEmployeeIds);
        var progressEntries = await LoadProgressEntriesAsync(goalRows.Select(x => x.Assignment.Id).ToList());
        var assessments = await LoadAssessmentsAsync(selectedCycle.Id, participantEmployeeIds);
        var assessmentItems = await LoadAssessmentItemsAsync(assessments);
        var models = await LoadModelsAsync(assessments);
        var modelById = models.ToDictionary(x => x.Id, x => x);
        var modelItems = await LoadModelItemsAsync(models);

        var goalIssues = BuildGoalIssues(goalRows, progressEntries, employeeById);
        var assessmentIssues = BuildAssessmentIssues(assessments, assessmentItems, modelItems, employeeById, modelById, participantEmployeeIds);
        var latestProgressValues = BuildLatestProgressValues(goalRows, progressEntries);

        dto.ParticipantsInCycleCount = participants.Count;
        dto.GoalAssignmentCount = goalRows.Count;
        dto.GoalsWithoutProgressCount = goalIssues.Count(x => x.Issue.Contains("progress", StringComparison.OrdinalIgnoreCase));
        dto.OverdueGoalCount = goalIssues.Count(x => x.Issue.Contains("ritardo", StringComparison.OrdinalIgnoreCase));
        dto.AssessmentCount = assessments.Count;
        dto.AssessmentIssueCount = assessmentIssues.Count;
        dto.AverageLatestProgressPercent = latestProgressValues.Count == 0 ? null : latestProgressValues.Average();
        dto.Phases = BuildPhaseRows(participants, phaseById);
        dto.GoalStatuses = BuildGoalStatuses(goalRows);
        dto.GoalIssues = goalIssues;
        dto.AssessmentIssues = assessmentIssues;
        dto.Members = BuildMembers(managedEmployees, participants, phaseById, goalRows, progressEntries, assessments, assessmentIssues);

        return dto;
    }

    private async Task<Employee> ResolveActorEmployeeAsync()
    {
        if (_currentUser.Id is null)
        {
            throw new BusinessException("UserNotAuthenticated");
        }

        var currentUserId = _currentUser.Id.Value;
        var employee = await _asyncExecuter.FirstOrDefaultAsync(
            (await _employeeRepository.GetQueryableAsync()).Where(x => x.UserId == currentUserId));

        if (employee is null)
        {
            throw new BusinessException("EmployeeNotLinkedToUser");
        }

        return employee;
    }

    private async Task<List<Employee>> LoadManagedEmployeesAsync(List<Guid> managedEmployeeIds)
    {
        if (managedEmployeeIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _employeeRepository.GetQueryableAsync())
                .Where(x => managedEmployeeIds.Contains(x.Id))
                .OrderBy(x => x.FullName));
    }

    private async Task<List<Cycle>> LoadCyclesAsync()
    {
        return await _asyncExecuter.ToListAsync(
            (await _cycleRepository.GetQueryableAsync())
                .OrderByDescending(x => x.CycleYear)
                .ThenByDescending(x => x.CreationTime)
                .ThenBy(x => x.Name));
    }

    private async Task<List<CycleParticipant>> LoadParticipantsAsync(Guid cycleId, List<Guid> managedEmployeeIds)
    {
        return await _asyncExecuter.ToListAsync(
            (await _participantRepository.GetQueryableAsync())
                .Where(x => x.CycleId == cycleId && managedEmployeeIds.Contains(x.EmployeeId)));
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

    private async Task<List<TeamGoalRow>> LoadGoalRowsAsync(Guid cycleId, List<Guid> employeeIds)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var assignmentQuery = await _goalAssignmentRepository.GetQueryableAsync();
        var goalQuery = await _goalRepository.GetQueryableAsync();

        var rows = await _asyncExecuter.ToListAsync(
            from assignment in assignmentQuery
            join goal in goalQuery on assignment.GoalId equals goal.Id
            where assignment.CycleId == cycleId && employeeIds.Contains(assignment.EmployeeId)
            select new
            {
                Assignment = assignment,
                Goal = goal
            });

        return rows
            .Select(x => new TeamGoalRow(x.Assignment, x.Goal))
            .ToList();
    }

    private async Task<List<GoalProgressEntry>> LoadProgressEntriesAsync(List<Guid> assignmentIds)
    {
        assignmentIds = assignmentIds.Distinct().ToList();
        if (assignmentIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _goalProgressEntryRepository.GetQueryableAsync())
                .Where(x => assignmentIds.Contains(x.AssignmentId)));
    }

    private async Task<List<CompetencyAssessment>> LoadAssessmentsAsync(Guid cycleId, List<Guid> employeeIds)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _assessmentRepository.GetQueryableAsync())
                .Where(x => x.CycleId == cycleId && employeeIds.Contains(x.EmployeeId)));
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

    private List<TeamReportGoalIssueDto> BuildGoalIssues(
        List<TeamGoalRow> goalRows,
        List<GoalProgressEntry> progressEntries,
        Dictionary<Guid, Employee> employeeById)
    {
        var today = DateOnly.FromDateTime(Clock.Now);
        var progressByAssignmentId = progressEntries
            .GroupBy(x => x.AssignmentId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.EntryDate).ToList());
        var rows = new List<TeamReportGoalIssueDto>();

        foreach (var row in goalRows)
        {
            progressByAssignmentId.TryGetValue(row.Assignment.Id, out var entries);
            entries ??= [];
            var latest = entries.FirstOrDefault();
            var isOverdue = row.Assignment.DueDate.HasValue &&
                            row.Assignment.DueDate.Value < today &&
                            !string.Equals(row.Assignment.Status, "Closed", StringComparison.OrdinalIgnoreCase);

            if (latest is null)
            {
                rows.Add(MapGoalIssue(row, employeeById, latest, "Goal senza progress registrato."));
            }

            if (isOverdue)
            {
                rows.Add(MapGoalIssue(row, employeeById, latest, "Goal in ritardo rispetto alla due date."));
            }
        }

        return rows
            .OrderBy(x => x.EmployeeName)
            .ThenBy(x => x.GoalTitle)
            .ThenBy(x => x.Issue)
            .ToList();
    }

    private static TeamReportGoalIssueDto MapGoalIssue(
        TeamGoalRow row,
        Dictionary<Guid, Employee> employeeById,
        GoalProgressEntry? latest,
        string issue)
    {
        employeeById.TryGetValue(row.Assignment.EmployeeId, out var employee);

        return new TeamReportGoalIssueDto
        {
            EmployeeId = row.Assignment.EmployeeId,
            EmployeeName = employee?.FullName ?? string.Empty,
            AssignmentId = row.Assignment.Id,
            GoalTitle = row.Goal.Title,
            Status = row.Assignment.Status,
            DueDate = row.Assignment.DueDate,
            LastProgressPercent = latest?.ProgressPercent,
            LastProgressDate = latest?.EntryDate,
            Issue = issue
        };
    }

    private static List<TeamReportAssessmentIssueDto> BuildAssessmentIssues(
        List<CompetencyAssessment> assessments,
        List<CompetencyAssessmentItem> assessmentItems,
        List<CompetencyModelItem> modelItems,
        Dictionary<Guid, Employee> employeeById,
        Dictionary<Guid, CompetencyModel> modelById,
        List<Guid> participantEmployeeIds)
    {
        var issues = new List<TeamReportAssessmentIssueDto>();
        var managerAssessmentsByEmployeeId = assessments
            .Where(x => string.Equals(x.AssessmentType, "Manager", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var itemsByAssessmentId = assessmentItems
            .GroupBy(x => x.AssessmentId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var requiredModelItemsByModelId = modelItems
            .Where(x => x.IsRequired)
            .GroupBy(x => x.ModelId)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var employeeId in participantEmployeeIds)
        {
            employeeById.TryGetValue(employeeId, out var employee);
            if (!managerAssessmentsByEmployeeId.TryGetValue(employeeId, out var employeeManagerAssessments) ||
                employeeManagerAssessments.Count == 0)
            {
                issues.Add(new TeamReportAssessmentIssueDto
                {
                    EmployeeId = employeeId,
                    EmployeeName = employee?.FullName ?? string.Empty,
                    Email = employee?.Email,
                    AssessmentType = "Manager",
                    Status = "-",
                    Issue = "Assessment Manager mancante per il participant."
                });
            }
        }

        foreach (var assessment in assessments)
        {
            if (!assessment.ModelId.HasValue)
            {
                continue;
            }

            requiredModelItemsByModelId.TryGetValue(assessment.ModelId.Value, out var requiredItems);
            itemsByAssessmentId.TryGetValue(assessment.Id, out var items);
            modelById.TryGetValue(assessment.ModelId.Value, out var model);
            employeeById.TryGetValue(assessment.EmployeeId, out var employee);

            requiredItems ??= [];
            items ??= [];
            var itemByCompetencyId = items
                .GroupBy(x => x.CompetencyId)
                .ToDictionary(x => x.Key, x => x.First());
            var missingRequiredCount = requiredItems.Count(x =>
                !itemByCompetencyId.TryGetValue(x.CompetencyId, out var item) ||
                !item.Score.HasValue);

            if (missingRequiredCount == 0)
            {
                continue;
            }

            issues.Add(new TeamReportAssessmentIssueDto
            {
                EmployeeId = assessment.EmployeeId,
                EmployeeName = employee?.FullName ?? string.Empty,
                Email = employee?.Email,
                AssessmentId = assessment.Id,
                AssessmentType = assessment.AssessmentType,
                Status = assessment.Status,
                ModelName = model?.Name ?? string.Empty,
                RequiredItemsCount = requiredItems.Count,
                MissingRequiredCount = missingRequiredCount,
                Issue = "Required mancanti o senza score."
            });
        }

        return issues
            .OrderBy(x => x.EmployeeName)
            .ThenBy(x => x.AssessmentType)
            .ThenBy(x => x.Issue)
            .ToList();
    }

    private static List<decimal> BuildLatestProgressValues(
        List<TeamGoalRow> goalRows,
        List<GoalProgressEntry> progressEntries)
    {
        var progressByAssignmentId = progressEntries
            .GroupBy(x => x.AssignmentId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.EntryDate).FirstOrDefault());

        return goalRows
            .Select(x => progressByAssignmentId.GetValueOrDefault(x.Assignment.Id)?.ProgressPercent)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
    }

    private static List<TeamReportMemberDto> BuildMembers(
        List<Employee> employees,
        List<CycleParticipant> participants,
        Dictionary<Guid, ProcessPhase> phaseById,
        List<TeamGoalRow> goalRows,
        List<GoalProgressEntry> progressEntries,
        List<CompetencyAssessment> assessments,
        List<TeamReportAssessmentIssueDto> assessmentIssues)
    {
        var participantByEmployeeId = participants
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.First());
        var goalCountByEmployeeId = goalRows
            .GroupBy(x => x.Assignment.EmployeeId)
            .ToDictionary(x => x.Key, x => x.Count());
        var assessmentCountByEmployeeId = assessments
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.Count());
        var missingAssessmentCountByEmployeeId = assessmentIssues
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.Count());
        var latestProgressByAssignmentId = progressEntries
            .GroupBy(x => x.AssignmentId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.EntryDate).FirstOrDefault());
        var averageProgressByEmployeeId = goalRows
            .Select(x => new
            {
                x.Assignment.EmployeeId,
                Progress = latestProgressByAssignmentId.GetValueOrDefault(x.Assignment.Id)?.ProgressPercent
            })
            .Where(x => x.Progress.HasValue)
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => (decimal?)x.Average(v => v.Progress!.Value));

        return employees
            .Select(employee =>
            {
                participantByEmployeeId.TryGetValue(employee.Id, out var participant);
                ProcessPhase? phase = null;
                if (participant?.CurrentPhaseId.HasValue == true)
                {
                    phaseById.TryGetValue(participant.CurrentPhaseId.Value, out phase);
                }

                return new TeamReportMemberDto
                {
                    EmployeeId = employee.Id,
                    FullName = employee.FullName,
                    Email = employee.Email,
                    ParticipantStatus = participant?.Status ?? "NotInCycle",
                    CurrentPhaseCode = phase?.Code ?? "-",
                    CurrentPhaseName = phase?.Name ?? "Nessuna fase",
                    GoalAssignmentCount = goalCountByEmployeeId.GetValueOrDefault(employee.Id),
                    AssessmentCount = assessmentCountByEmployeeId.GetValueOrDefault(employee.Id),
                    MissingRequiredAssessmentCount = missingAssessmentCountByEmployeeId.GetValueOrDefault(employee.Id),
                    AverageLatestProgressPercent = averageProgressByEmployeeId.GetValueOrDefault(employee.Id)
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();
    }

    private static List<TeamReportPhaseDto> BuildPhaseRows(
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

                return new TeamReportPhaseDto
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

    private static List<TeamReportGoalStatusDto> BuildGoalStatuses(List<TeamGoalRow> goalRows)
    {
        return goalRows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Assignment.Status) ? "-" : x.Assignment.Status)
            .Select(x => new TeamReportGoalStatusDto
            {
                Status = x.Key,
                Count = x.Count()
            })
            .OrderBy(x => x.Status)
            .ToList();
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
                throw new BusinessException("TeamReportCycleNotFound");
            }

            return selected;
        }

        return cycles
            .OrderByDescending(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.CycleYear)
            .ThenByDescending(x => x.CreationTime)
            .First();
    }

    private sealed record TeamGoalRow(GoalAssignment Assignment, Goal Goal);
}

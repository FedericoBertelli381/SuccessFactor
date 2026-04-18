using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Competencies.Assessments;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Goals;
using SuccessFactor.JobRoles;
using SuccessFactor.OrgUnits;
using SuccessFactor.Security;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Hr;

[Authorize]
public class PerformanceDashboardAppService : ApplicationService, IPerformanceDashboardAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _participantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<OrgUnit, Guid> _orgUnitRepository;
    private readonly IRepository<JobRole, Guid> _jobRoleRepository;
    private readonly IRepository<GoalAssignment, Guid> _goalAssignmentRepository;
    private readonly IRepository<GoalProgressEntry, Guid> _goalProgressEntryRepository;
    private readonly IRepository<CompetencyAssessment, Guid> _assessmentRepository;
    private readonly IRepository<CompetencyAssessmentItem, Guid> _assessmentItemRepository;

    public PerformanceDashboardAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> participantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<OrgUnit, Guid> orgUnitRepository,
        IRepository<JobRole, Guid> jobRoleRepository,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository,
        IRepository<GoalProgressEntry, Guid> goalProgressEntryRepository,
        IRepository<CompetencyAssessment, Guid> assessmentRepository,
        IRepository<CompetencyAssessmentItem, Guid> assessmentItemRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _employeeRepository = employeeRepository;
        _orgUnitRepository = orgUnitRepository;
        _jobRoleRepository = jobRoleRepository;
        _goalAssignmentRepository = goalAssignmentRepository;
        _goalProgressEntryRepository = goalProgressEntryRepository;
        _assessmentRepository = assessmentRepository;
        _assessmentItemRepository = assessmentItemRepository;
    }

    public async Task<PerformanceDashboardDto> GetAsync(GetPerformanceDashboardInput input)
    {
        EnsureTenantAndHrOrAdmin();
        input ??= new GetPerformanceDashboardInput();

        var cycles = await _asyncExecuter.ToListAsync(
            (await _cycleRepository.GetQueryableAsync())
                .OrderByDescending(x => x.CycleYear)
                .ThenByDescending(x => x.CreationTime)
                .ThenBy(x => x.Name));
        var selectedCycle = ResolveSelectedCycle(cycles, input.CycleId);

        var dto = new PerformanceDashboardDto
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
        var employees = await LoadEmployeesAsync(participantEmployeeIds);
        var employeeById = employees.ToDictionary(x => x.Id, x => x);
        var orgUnits = await _asyncExecuter.ToListAsync(await _orgUnitRepository.GetQueryableAsync());
        var jobRoles = await _asyncExecuter.ToListAsync(await _jobRoleRepository.GetQueryableAsync());
        var orgUnitById = orgUnits.ToDictionary(x => x.Id, x => x.Name);
        var jobRoleById = jobRoles.ToDictionary(x => x.Id, x => x.Name);

        var assignments = await LoadGoalAssignmentsAsync(selectedCycle.Id, participantEmployeeIds);
        var progressEntries = await LoadProgressEntriesAsync(assignments.Select(x => x.Id).ToList());
        var latestProgressByAssignmentId = progressEntries
            .GroupBy(x => x.AssignmentId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.EntryDate).FirstOrDefault());

        var assessments = await LoadAssessmentsAsync(selectedCycle.Id, participantEmployeeIds);
        var assessmentItems = await LoadAssessmentItemsAsync(assessments);
        var scoredItems = assessmentItems.Where(x => x.Score.HasValue).ToList();

        dto.ParticipantCount = participants.Count;
        dto.CompletedParticipantCount = participants.Count(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        dto.ParticipantCompletionPercent = Percent(dto.CompletedParticipantCount, dto.ParticipantCount);
        dto.GoalAssignmentCount = assignments.Count;
        dto.AverageLatestProgressPercent = AverageLatestProgress(assignments, latestProgressByAssignmentId);
        dto.AssessmentCount = assessments.Count;
        dto.SubmittedOrClosedAssessmentCount = assessments.Count(IsSubmittedOrClosed);
        dto.AssessmentCompletionPercent = Percent(dto.SubmittedOrClosedAssessmentCount, dto.AssessmentCount);
        dto.AverageScore = scoredItems.Count == 0 ? null : scoredItems.Average(x => (decimal)x.Score!.Value);
        dto.ScoreDistribution = BuildScoreDistribution(scoredItems);
        dto.OrgUnitBreakdown = BuildBreakdown(
            employees,
            assignments,
            latestProgressByAssignmentId,
            assessments,
            scoredItems,
            employee => employee.OrgUnitId,
            groupId => groupId.HasValue && orgUnitById.TryGetValue(groupId.Value, out var name) ? name : "Senza OrgUnit");
        dto.JobRoleBreakdown = BuildBreakdown(
            employees,
            assignments,
            latestProgressByAssignmentId,
            assessments,
            scoredItems,
            employee => employee.JobRoleId,
            groupId => groupId.HasValue && jobRoleById.TryGetValue(groupId.Value, out var name) ? name : "Senza JobRole");

        return dto;
    }

    private async Task<List<Employee>> LoadEmployeesAsync(List<Guid> employeeIds)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _employeeRepository.GetQueryableAsync())
                .Where(x => employeeIds.Contains(x.Id)));
    }

    private async Task<List<GoalAssignment>> LoadGoalAssignmentsAsync(Guid cycleId, List<Guid> employeeIds)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        return await _asyncExecuter.ToListAsync(
            (await _goalAssignmentRepository.GetQueryableAsync())
                .Where(x => x.CycleId == cycleId && employeeIds.Contains(x.EmployeeId)));
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

    private static List<PerformanceDashboardBreakdownDto> BuildBreakdown(
        List<Employee> employees,
        List<GoalAssignment> assignments,
        Dictionary<Guid, GoalProgressEntry?> latestProgressByAssignmentId,
        List<CompetencyAssessment> assessments,
        List<CompetencyAssessmentItem> scoredItems,
        Func<Employee, Guid?> groupSelector,
        Func<Guid?, string> nameResolver)
    {
        var employeeGroups = employees
            .GroupBy(groupSelector)
            .Select(x => new
            {
                GroupId = x.Key,
                EmployeeIds = x.Select(e => e.Id).ToHashSet()
            })
            .ToList();
        var assessmentById = assessments.ToDictionary(x => x.Id, x => x);

        return employeeGroups
            .Select(group =>
            {
                var employeeIds = group.EmployeeIds;
                var groupAssignments = assignments.Where(x => employeeIds.Contains(x.EmployeeId)).ToList();
                var groupAssessments = assessments.Where(x => employeeIds.Contains(x.EmployeeId)).ToList();
                var groupScoredItems = scoredItems
                    .Where(x => assessmentById.TryGetValue(x.AssessmentId, out var assessment) &&
                                employeeIds.Contains(assessment.EmployeeId))
                    .ToList();

                return new PerformanceDashboardBreakdownDto
                {
                    GroupId = group.GroupId,
                    GroupName = nameResolver(group.GroupId),
                    ParticipantCount = employeeIds.Count,
                    GoalAssignmentCount = groupAssignments.Count,
                    AverageLatestProgressPercent = AverageLatestProgress(groupAssignments, latestProgressByAssignmentId),
                    AssessmentCount = groupAssessments.Count,
                    AssessmentCompletionPercent = Percent(groupAssessments.Count(IsSubmittedOrClosed), groupAssessments.Count),
                    AverageScore = groupScoredItems.Count == 0 ? null : groupScoredItems.Average(x => (decimal)x.Score!.Value)
                };
            })
            .OrderByDescending(x => x.ParticipantCount)
            .ThenBy(x => x.GroupName)
            .ToList();
    }

    private static List<PerformanceDashboardScoreBucketDto> BuildScoreDistribution(List<CompetencyAssessmentItem> scoredItems)
    {
        return scoredItems
            .Where(x => x.Score.HasValue)
            .GroupBy(x => x.Score!.Value)
            .Select(x => new PerformanceDashboardScoreBucketDto
            {
                Score = x.Key,
                Count = x.Count()
            })
            .OrderBy(x => x.Score)
            .ToList();
    }

    private static decimal? AverageLatestProgress(
        List<GoalAssignment> assignments,
        Dictionary<Guid, GoalProgressEntry?> latestProgressByAssignmentId)
    {
        var values = assignments
            .Select(x => latestProgressByAssignmentId.GetValueOrDefault(x.Id)?.ProgressPercent)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        return values.Count == 0 ? null : values.Average();
    }

    private void EnsureTenantAndHrOrAdmin()
    {
        if (CurrentTenant.Id is null)
        {
            throw new BusinessException("TenantMissing");
        }

        var roles = _currentUser.Roles ?? Array.Empty<string>();
        var isAllowed = SuccessFactorRoles.IsAdminOrHr(roles);

        if (!isAllowed)
        {
            throw new BusinessException("CurrentUserIsNotHr");
        }
    }

    private static bool IsSubmittedOrClosed(CompetencyAssessment assessment)
        => string.Equals(assessment.Status, "Submitted", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(assessment.Status, "Closed", StringComparison.OrdinalIgnoreCase);

    private static decimal Percent(int numerator, int denominator)
        => denominator == 0 ? 0m : Math.Round(numerator * 100m / denominator, 2);

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

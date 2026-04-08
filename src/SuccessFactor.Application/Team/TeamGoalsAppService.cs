using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Goals;
using SuccessFactor.My.Dtos;
using SuccessFactor.My.Support;
using SuccessFactor.Team.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.Team;

[Authorize]
public class TeamGoalsAppService : ApplicationService, ITeamGoalsAppService
{
    private const string GoalFieldProgressPercent = "Goals.ProgressPercent";
    private const string GoalFieldActualValue = "Goals.ActualValue";
    private const string GoalFieldNote = "Goals.Note";
    private const string GoalFieldAttachmentId = "Goals.AttachmentId";

    private readonly ITeamWorkflowContextResolver _teamWorkflowContextResolver;
    private readonly IPhasePermissionResolver _phasePermissionResolver;

    private readonly IRepository<GoalAssignment, Guid> _goalAssignmentRepository;
    private readonly IRepository<Goal, Guid> _goalRepository;
    private readonly IRepository<GoalProgressEntry, Guid> _goalProgressEntryRepository;

    public TeamGoalsAppService(
        ITeamWorkflowContextResolver teamWorkflowContextResolver,
        IPhasePermissionResolver phasePermissionResolver,
        IRepository<GoalAssignment, Guid> goalAssignmentRepository,
        IRepository<Goal, Guid> goalRepository,
        IRepository<GoalProgressEntry, Guid> goalProgressEntryRepository)
    {
        _teamWorkflowContextResolver = teamWorkflowContextResolver;
        _phasePermissionResolver = phasePermissionResolver;
        _goalAssignmentRepository = goalAssignmentRepository;
        _goalRepository = goalRepository;
        _goalProgressEntryRepository = goalProgressEntryRepository;
    }

    public async Task<MyGoalsDto> GetAsync(GetTeamGoalsInput input)
    {
        var context = await _teamWorkflowContextResolver.ResolveAsync(
            input.TargetEmployeeId,
            input.CycleId);

        var phasePermission = await _phasePermissionResolver.GetEffectivePhasePermissionAsync(
            context.Cycle.TemplateId,
            context.TargetParticipant.CurrentPhaseId!.Value,
            context.RoleCodeUsed);

        var goalFieldAccess = await _phasePermissionResolver.GetEffectiveFieldAccessAsync(
            context.Cycle.TemplateId,
            context.TargetParticipant.CurrentPhaseId.Value,
            context.RoleCodeUsed,
            GoalFieldProgressPercent,
            GoalFieldActualValue,
            GoalFieldNote,
            GoalFieldAttachmentId);

        var assignmentQuery = await _goalAssignmentRepository.GetQueryableAsync();
        var goalQuery = await _goalRepository.GetQueryableAsync();

        var rows = await AsyncExecuter.ToListAsync(
            from assignment in assignmentQuery
            join goal in goalQuery on assignment.GoalId equals goal.Id
            where assignment.CycleId == context.Cycle.Id
               && assignment.EmployeeId == context.TargetEmployee.Id
            orderby goal.Title
            select new
            {
                Assignment = assignment,
                Goal = goal
            });

        var assignmentIds = rows
            .Select(x => x.Assignment.Id)
            .Distinct()
            .ToList();

        var progressEntries = new List<GoalProgressEntry>();

        if (assignmentIds.Count > 0)
        {
            var progressQuery = await _goalProgressEntryRepository.GetQueryableAsync();

            progressEntries = await AsyncExecuter.ToListAsync(
                progressQuery.Where(x => assignmentIds.Contains(x.AssignmentId)));
        }

        var progressByAssignment = progressEntries
            .GroupBy(x => x.AssignmentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EntryDate).ToList());

        var canEdit = phasePermission?.CanEdit ?? false;

        var items = rows
            .Select(x =>
            {
                progressByAssignment.TryGetValue(x.Assignment.Id, out var entries);
                entries ??= new List<GoalProgressEntry>();

                var last = entries.FirstOrDefault();

                return new MyGoalItemDto
                {
                    AssignmentId = x.Assignment.Id,
                    GoalId = x.Goal.Id,
                    GoalName = x.Goal.Title,
                    Weight = x.Assignment.Weight,
                    Status = x.Assignment.Status,
                    TargetValue = x.Assignment.TargetValue,
                    DueDate = x.Assignment.DueDate,
                    CanEdit = canEdit,

                    ProgressPercentAccess = goalFieldAccess[GoalFieldProgressPercent],
                    ActualValueAccess = goalFieldAccess[GoalFieldActualValue],
                    NoteAccess = goalFieldAccess[GoalFieldNote],
                    AttachmentAccess = goalFieldAccess[GoalFieldAttachmentId],

                    LastProgress = last is null
                        ? null
                        : new MyGoalLastProgressDto
                        {
                            EntryDate = last.EntryDate,
                            ProgressPercent = last.ProgressPercent,
                            ActualValue = last.ActualValue,
                            Note = last.Note,
                            AttachmentId = last.AttachmentId
                        },

                    Summary = new MyGoalProgressSummaryDto
                    {
                        EntriesCount = entries.Count,
                        LastEntryDate = last?.EntryDate,
                        LastProgressPercent = last?.ProgressPercent,
                        LastActualValue = last?.ActualValue
                    }
                };
            })
            .ToList();

        return new MyGoalsDto
        {
            EmployeeId = context.TargetEmployee.Id,
            EmployeeName = context.TargetEmployee.FullName,
            CycleId = context.Cycle.Id,
            CycleName = context.Cycle.Name,
            CurrentPhaseId = context.TargetParticipant.CurrentPhaseId,
            CurrentPhaseCode = context.CurrentPhase?.Code,
            RoleCodeUsed = context.RoleCodeUsed,
            CanEdit = canEdit,
            Items = items
        };
    }
}
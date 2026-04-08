using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Cycles;
using SuccessFactor.My.Dtos;
using SuccessFactor.My.Support;
using SuccessFactor.Workflow.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.My;

[Authorize]
public class MyDashboardAppService : ApplicationService, IMyDashboardAppService
{
    private readonly IMyWorkflowContextResolver _workflowContextResolver;
    private readonly MyGoalsAppService _myGoalsAppService;
    private readonly MyAssessmentsAppService _myAssessmentsAppService;
    private readonly CycleWorkflowAppService _cycleWorkflowAppService;
    private readonly WorkflowAuthorizationService _workflowAuthorizationService;

    public MyDashboardAppService(
        IMyWorkflowContextResolver workflowContextResolver,
        MyGoalsAppService myGoalsAppService,
        MyAssessmentsAppService myAssessmentsAppService,
        CycleWorkflowAppService cycleWorkflowAppService,
        WorkflowAuthorizationService workflowAuthorizationService)
    {
        _workflowContextResolver = workflowContextResolver;
        _myGoalsAppService = myGoalsAppService;
        _myAssessmentsAppService = myAssessmentsAppService;
        _cycleWorkflowAppService = cycleWorkflowAppService;
        _workflowAuthorizationService = workflowAuthorizationService;
    }

    public async Task<MyDashboardDto> GetAsync(GetMyDashboardInput input)
    {
        input ??= new GetMyDashboardInput();

        var context = await _workflowContextResolver.ResolveAsync(input.CycleId);

        var goals = await _myGoalsAppService.GetAsync(new GetMyGoalsInput
        {
            CycleId = context.Cycle.Id
        });

        var assessments = await _myAssessmentsAppService.GetAsync(new GetMyAssessmentsInput
        {
            CycleId = context.Cycle.Id
        });

        var auth = await _workflowAuthorizationService.EvaluateAsync(
            context.Cycle.Id,
            context.Employee.Id);

        var openAssessments = assessments.Items
            .Where(x => !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dto = new MyDashboardDto
        {
            EmployeeId = context.Employee.Id,
            EmployeeName = context.Employee.FullName,
            CycleId = context.Cycle.Id,
            CycleName = context.Cycle.Name,
            CycleStatus = context.Cycle.Status,
            CurrentPhaseId = context.Participant.CurrentPhaseId,
            CurrentPhaseCode = context.CurrentPhase?.Code,
            RoleCodeUsed = context.RoleCodeUsed,

            CanEditGoals = goals.CanEdit,
            CanEditAssessments = assessments.Items.Any(x => x.CanEdit),
            CanSubmitAssessments = assessments.CanSubmitAny,
            CanAdvancePhase = auth.CanAdvance,

            GoalsCount = goals.Items.Count,
            EditableGoalsCount = goals.Items.Count(x => x.CanEdit),
            OpenAssessmentsCount = openAssessments.Count,
            MissingRequiredAssessmentsCount = openAssessments.Count(x => x.MissingRequiredCount > 0)
        };

        if (dto.EditableGoalsCount > 0)
        {
            dto.Todo.Add("Aggiorna i goal modificabili.");
        }

        if (dto.OpenAssessmentsCount > 0 && dto.CanEditAssessments)
        {
            dto.Todo.Add("Completa gli assessment aperti.");
        }

        if (dto.CanSubmitAssessments)
        {
            dto.Todo.Add("Invia gli assessment completi.");
        }

        if (dto.CanAdvancePhase)
        {
            dto.Todo.Add("Puoi avanzare alla fase successiva.");
        }

        return dto;
    }

    public async Task AdvancePhaseAsync(Guid cycleId)
    {
        var context = await _workflowContextResolver.ResolveAsync(cycleId);

        await _cycleWorkflowAppService.AdvancePhaseAsync(new AdvancePhaseDto
        {
            CycleId = context.Cycle.Id,
            EmployeeId = context.Employee.Id,
            ToPhaseId = null
        });
    }
}
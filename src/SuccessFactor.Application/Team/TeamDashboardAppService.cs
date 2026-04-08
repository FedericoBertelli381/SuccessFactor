using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Team.Support;
using SuccessFactor.Workflow.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Team;

[Authorize]
public class TeamDashboardAppService : ApplicationService, ITeamDashboardAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IManagerScopeResolver _managerScopeResolver;
    private readonly ITeamWorkflowContextResolver _teamWorkflowContextResolver;
    private readonly WorkflowAuthorizationService _workflowAuthorizationService;
    private readonly CycleWorkflowAppService _cycleWorkflowAppService;

    public TeamDashboardAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Employee, Guid> employeeRepository,
        IManagerScopeResolver managerScopeResolver,
        ITeamWorkflowContextResolver teamWorkflowContextResolver,
        WorkflowAuthorizationService workflowAuthorizationService,
        CycleWorkflowAppService cycleWorkflowAppService)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _employeeRepository = employeeRepository;
        _managerScopeResolver = managerScopeResolver;
        _teamWorkflowContextResolver = teamWorkflowContextResolver;
        _workflowAuthorizationService = workflowAuthorizationService;
        _cycleWorkflowAppService = cycleWorkflowAppService;
    }

    public async Task<TeamDashboardDto> GetAsync(GetTeamDashboardInput input)
    {
        input ??= new GetTeamDashboardInput();

        var actorEmployee = await ResolveActorEmployeeAsync();
        var managedEmployeeIds = await _managerScopeResolver.GetManagedEmployeeIdsAsync(actorEmployee.Id);

        var employeeQuery = await _employeeRepository.GetQueryableAsync();

        var managedEmployees = new List<Employee>();

        if (managedEmployeeIds.Count > 0)
        {
            managedEmployees = await _asyncExecuter.ToListAsync(
                employeeQuery
                    .Where(x => managedEmployeeIds.Contains(x.Id))
                    .OrderBy(x => x.FullName));
        }

        var selectedEmployeeId = input.TargetEmployeeId;

        if (!selectedEmployeeId.HasValue && managedEmployees.Count > 0)
        {
            selectedEmployeeId = managedEmployees[0].Id;
        }

        if (selectedEmployeeId.HasValue && !managedEmployeeIds.Contains(selectedEmployeeId.Value))
        {
            throw new BusinessException("TargetEmployeeNotInManagerScope");
        }

        var dto = new TeamDashboardDto
        {
            ActorEmployeeId = actorEmployee.Id,
            ActorEmployeeName = actorEmployee.FullName,
            TeamMembers = managedEmployees
                .Select(x => new TeamMemberDto
                {
                    EmployeeId = x.Id,
                    FullName = x.FullName,
                    Email = x.Email,
                    IsSelected = selectedEmployeeId.HasValue && x.Id == selectedEmployeeId.Value
                })
                .ToList()
        };

        if (!selectedEmployeeId.HasValue)
        {
            return dto;
        }

        var context = await _teamWorkflowContextResolver.ResolveAsync(selectedEmployeeId.Value, input.CycleId);

        var auth = await _workflowAuthorizationService.EvaluateAsync(
            context.Cycle.Id,
            context.TargetEmployee.Id);

        dto.CycleId = context.Cycle.Id;
        dto.CycleName = context.Cycle.Name;
        dto.CycleStatus = context.Cycle.Status;

        dto.SelectedEmployeeId = context.TargetEmployee.Id;
        dto.SelectedEmployeeName = context.TargetEmployee.FullName;

        dto.CurrentPhaseId = context.TargetParticipant.CurrentPhaseId;
        dto.CurrentPhaseCode = context.CurrentPhase?.Code;
        dto.RoleCodeUsed = context.RoleCodeUsed;

        dto.CanAdvancePhase = auth.CanAdvance;

        return dto;
    }

    public async Task AdvancePhaseAsync(Guid targetEmployeeId, Guid? cycleId)
    {
        var context = await _teamWorkflowContextResolver.ResolveAsync(targetEmployeeId, cycleId);

        await _cycleWorkflowAppService.AdvancePhaseAsync(new AdvancePhaseDto
        {
            CycleId = context.Cycle.Id,
            EmployeeId = context.TargetEmployee.Id,
            ToPhaseId = null
        });
    }

    private async Task<Employee> ResolveActorEmployeeAsync()
    {
        if (_currentUser.Id is null)
        {
            throw new BusinessException("UserNotAuthenticated");
        }

        var currentUserId = _currentUser.Id.Value;

        var employeeQuery = await _employeeRepository.GetQueryableAsync();

        var actorEmployee = await _asyncExecuter.FirstOrDefaultAsync(
            employeeQuery.Where(x => x.UserId == currentUserId));

        if (actorEmployee is null)
        {
            throw new BusinessException("EmployeeNotLinkedToUser");
        }

        return actorEmployee;
    }
}
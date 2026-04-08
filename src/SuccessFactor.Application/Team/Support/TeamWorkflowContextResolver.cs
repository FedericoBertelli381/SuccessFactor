using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Workflow;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Team.Support;

public class TeamWorkflowContextResolver : ITeamWorkflowContextResolver, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _cycleParticipantRepository;
    private readonly IRepository<ProcessPhase, Guid> _processPhaseRepository;

    private readonly IManagerScopeResolver _managerScopeResolver;

    public TeamWorkflowContextResolver(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> cycleParticipantRepository,
        IRepository<ProcessPhase, Guid> processPhaseRepository,
        IManagerScopeResolver managerScopeResolver)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _employeeRepository = employeeRepository;
        _cycleRepository = cycleRepository;
        _cycleParticipantRepository = cycleParticipantRepository;
        _processPhaseRepository = processPhaseRepository;
        _managerScopeResolver = managerScopeResolver;
    }

    public async Task<TeamWorkflowContext> ResolveAsync(Guid targetEmployeeId, Guid? cycleId)
    {
        if (_currentUser.Id is null)
        {
            throw new BusinessException("UserNotAuthenticated");
        }

        var currentUserId = _currentUser.Id.Value;

        // 1) actor employee = employee collegato all'utente corrente
        var employeeQuery = await _employeeRepository.GetQueryableAsync();

        var actorEmployee = await _asyncExecuter.FirstOrDefaultAsync(
            employeeQuery.Where(x => x.UserId == currentUserId));

        if (actorEmployee is null)
        {
            throw new BusinessException("EmployeeNotLinkedToUser");
        }

        // 2) target employee
        var targetEmployee = await _asyncExecuter.FirstOrDefaultAsync(
            employeeQuery.Where(x => x.Id == targetEmployeeId));

        if (targetEmployee is null)
        {
            throw new BusinessException("EmployeeNotFound");
        }

        if (!targetEmployee.IsActive)
        {
            throw new BusinessException("EmployeeNotActive");
        }

        // 3) verifica scope manageriale
        var managedEmployeeIds = await _managerScopeResolver.GetManagedEmployeeIdsAsync(actorEmployee.Id);

        if (!managedEmployeeIds.Contains(targetEmployeeId))
        {
            throw new BusinessException("TargetEmployeeNotInManagerScope");
        }

        // 4) cycle
        var cycleQuery = await _cycleRepository.GetQueryableAsync();

        Cycle? cycle;

        if (cycleId.HasValue)
        {
            cycle = await _asyncExecuter.FirstOrDefaultAsync(
                cycleQuery.Where(x => x.Id == cycleId.Value));
        }
        else
        {
            cycle = await _asyncExecuter.FirstOrDefaultAsync(
                cycleQuery
                    .Where(x => x.Status == "Active")
                    .OrderByDescending(x => x.CycleYear));
        }

        if (cycle is null)
        {
            throw new BusinessException("CycleNotFound");
        }

        // 5) participant del target nel ciclo
        var participantQuery = await _cycleParticipantRepository.GetQueryableAsync();

        var participant = await _asyncExecuter.FirstOrDefaultAsync(
            participantQuery.Where(x =>
                x.CycleId == cycle.Id &&
                x.EmployeeId == targetEmployee.Id));

        if (participant is null)
        {
            throw new BusinessException("ParticipantNotFound");
        }

        if (participant.CurrentPhaseId is null)
        {
            throw new BusinessException("ParticipantHasNoPhase");
        }

        // 6) phase del target
        var phaseQuery = await _processPhaseRepository.GetQueryableAsync();

        var currentPhase = await _asyncExecuter.FirstOrDefaultAsync(
            phaseQuery.Where(x => x.Id == participant.CurrentPhaseId.Value));

        return new TeamWorkflowContext
        {
            ActorEmployee = actorEmployee,
            TargetEmployee = targetEmployee,
            Cycle = cycle,
            TargetParticipant = participant,
            CurrentPhase = currentPhase,
            RoleCodeUsed = "Manager"
        };
    }
}
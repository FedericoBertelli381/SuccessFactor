using SuccessFactor.Cycles;
// TODO: sostituisci questi using con i namespace reali delle tue entity
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

namespace SuccessFactor.My.Support;

public class MyWorkflowContextResolver : IMyWorkflowContextResolver, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _cycleParticipantRepository;
    private readonly IRepository<ProcessPhase, Guid> _processPhaseRepository;

    public MyWorkflowContextResolver(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> cycleParticipantRepository,
        IRepository<ProcessPhase, Guid> processPhaseRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _employeeRepository = employeeRepository;
        _cycleRepository = cycleRepository;
        _cycleParticipantRepository = cycleParticipantRepository;
        _processPhaseRepository = processPhaseRepository;
    }

    public async Task<MyWorkflowContext> ResolveAsync(Guid? cycleId)
    {
        if (_currentUser.Id is null)
        {
            throw new BusinessException("UserNotAuthenticated");
        }

        var currentUserId = _currentUser.Id.Value;

        // 1) Employee
        var employeeQuery = await _employeeRepository.GetQueryableAsync();

        var employee = await _asyncExecuter.FirstOrDefaultAsync(
            employeeQuery.Where(x => x.UserId == currentUserId));

        if (employee is null)
        {
            throw new BusinessException("EmployeeNotLinkedToUser");
        }

        // 2) Cycle
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

        // 3) Participant
        var participantQuery = await _cycleParticipantRepository.GetQueryableAsync();

        var participant = await _asyncExecuter.FirstOrDefaultAsync(
            participantQuery.Where(x =>
                x.CycleId == cycle.Id &&
                x.EmployeeId == employee.Id));

        if (participant is null)
        {
            throw new BusinessException("ParticipantNotFound");
        }

        if (participant.CurrentPhaseId is null)
        {
            throw new BusinessException("ParticipantHasNoPhase");
        }

        // 4) Phase
        var phaseQuery = await _processPhaseRepository.GetQueryableAsync();

        var currentPhase = await _asyncExecuter.FirstOrDefaultAsync(
            phaseQuery.Where(x => x.Id == participant.CurrentPhaseId.Value));

        // 5) RoleCode self-facing
        var roleCode = ResolveSelfRoleCode();

        return new MyWorkflowContext
        {
            Employee = employee,
            Cycle = cycle,
            Participant = participant,
            CurrentPhase = currentPhase,
            RoleCodeUsed = roleCode
        };
    }

    private string ResolveSelfRoleCode()
    {
        var hasHrRole = _currentUser.Roles.Any(r =>
            r.Contains("hr", StringComparison.OrdinalIgnoreCase));

        return hasHrRole ? "HR" : "Employee";
    }
}
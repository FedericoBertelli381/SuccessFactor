using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Workflow;

namespace SuccessFactor.Team.Support;

public class TeamWorkflowContext
{
    public Employee ActorEmployee { get; init; } = default!;

    public Employee TargetEmployee { get; init; } = default!;

    public Cycle Cycle { get; init; } = default!;

    public CycleParticipant TargetParticipant { get; init; } = default!;

    public ProcessPhase? CurrentPhase { get; init; }

    public string RoleCodeUsed { get; init; } = string.Empty;
}
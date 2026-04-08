using SuccessFactor.Cycles;
// TODO: sostituisci questi using con i namespace reali delle tue entity
using SuccessFactor.Employees;
using SuccessFactor.Workflow;
using System;

namespace SuccessFactor.My.Support;

public class MyWorkflowContext
{
    public Employee Employee { get; init; } = default!;

    public Cycle Cycle { get; init; } = default!;

    public CycleParticipant Participant { get; init; } = default!;

    public ProcessPhase? CurrentPhase { get; init; }

    public string RoleCodeUsed { get; init; } = string.Empty;
}
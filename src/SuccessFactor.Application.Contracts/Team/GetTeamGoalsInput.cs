using System;

namespace SuccessFactor.Team;

public class GetTeamGoalsInput
{
    public Guid TargetEmployeeId { get; set; }
    public Guid? CycleId { get; set; }
}
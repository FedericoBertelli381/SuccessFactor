using System;

namespace SuccessFactor.Team;

public class GetTeamDashboardInput
{
    public Guid? CycleId { get; set; }
    public Guid? TargetEmployeeId { get; set; }
}
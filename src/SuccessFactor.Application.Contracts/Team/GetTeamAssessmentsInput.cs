using System;

namespace SuccessFactor.Team;

public class GetTeamAssessmentsInput
{
    public Guid TargetEmployeeId { get; set; }
    public Guid? CycleId { get; set; }
    public bool OnlyOpen { get; set; }
}
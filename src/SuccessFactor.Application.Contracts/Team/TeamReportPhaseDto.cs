using System;

namespace SuccessFactor.Team;

public class TeamReportPhaseDto
{
    public Guid? PhaseId { get; set; }
    public string PhaseCode { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public int Count { get; set; }
}

using System;

namespace SuccessFactor.Team;

public class TeamReportCycleLookupDto
{
    public Guid CycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public string CycleStatus { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

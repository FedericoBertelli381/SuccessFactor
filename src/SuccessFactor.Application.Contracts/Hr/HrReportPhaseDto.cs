using System;

namespace SuccessFactor.Hr;

public class HrReportPhaseDto
{
    public Guid? PhaseId { get; set; }
    public string PhaseCode { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public int Count { get; set; }
}

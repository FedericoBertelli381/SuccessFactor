using System;

namespace SuccessFactor.Admin;

public class WorkflowPhaseLookupDto
{
    public Guid PhaseId { get; set; }
    public string PhaseCode { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public int PhaseOrder { get; set; }
    public bool IsTerminal { get; set; }
}

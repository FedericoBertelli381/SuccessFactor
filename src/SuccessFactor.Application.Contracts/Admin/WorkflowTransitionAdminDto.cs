using System;

namespace SuccessFactor.Admin;

public class WorkflowTransitionAdminDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public Guid FromPhaseId { get; set; }
    public string FromPhaseCode { get; set; } = string.Empty;
    public string FromPhaseName { get; set; } = string.Empty;
    public Guid ToPhaseId { get; set; }
    public string ToPhaseCode { get; set; } = string.Empty;
    public string ToPhaseName { get; set; } = string.Empty;
    public string? ConditionExpr { get; set; }
}

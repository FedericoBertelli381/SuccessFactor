using System;

namespace SuccessFactor.Admin;

public class CycleAdminListItemDto
{
    public Guid CycleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CycleYear { get; set; }
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public Guid? CurrentPhaseId { get; set; }
    public string? CurrentPhaseCode { get; set; }
    public string? CurrentPhaseName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

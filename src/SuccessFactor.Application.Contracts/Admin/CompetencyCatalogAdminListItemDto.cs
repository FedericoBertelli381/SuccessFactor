using System;

namespace SuccessFactor.Admin;

public class CompetencyCatalogAdminListItemDto
{
    public Guid CompetencyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ModelItemCount { get; set; }
    public int AssessmentItemCount { get; set; }
    public bool CanDelete { get; set; }
}

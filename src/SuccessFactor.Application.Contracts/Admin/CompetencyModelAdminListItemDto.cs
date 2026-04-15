using System;

namespace SuccessFactor.Admin;

public class CompetencyModelAdminListItemDto
{
    public Guid ModelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScaleType { get; set; } = string.Empty;
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
    public int ItemCount { get; set; }
    public int RequiredItemCount { get; set; }
    public decimal? TotalWeight { get; set; }
    public int AssessmentCount { get; set; }
    public bool CanEditStructure { get; set; }
    public bool CanDelete { get; set; }
}

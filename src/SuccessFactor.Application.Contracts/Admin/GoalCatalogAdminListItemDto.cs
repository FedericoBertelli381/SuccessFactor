using System;

namespace SuccessFactor.Admin;

public class GoalCatalogAdminListItemDto
{
    public Guid GoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsLibraryItem { get; set; }
    public decimal? DefaultWeight { get; set; }
    public int AssignmentCount { get; set; }
    public bool CanDelete { get; set; }
}

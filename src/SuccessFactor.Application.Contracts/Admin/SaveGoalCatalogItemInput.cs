using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveGoalCatalogItemInput
{
    [Required]
    [StringLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsLibraryItem { get; set; } = true;

    public decimal? DefaultWeight { get; set; }
}

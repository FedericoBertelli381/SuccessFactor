using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Goals;

public class CreateUpdateGoalDto
{
    [Required]
    [StringLength(300)]
    public string Title { get; set; } = default!;

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsLibraryItem { get; set; }

    public decimal? DefaultWeight { get; set; }
}
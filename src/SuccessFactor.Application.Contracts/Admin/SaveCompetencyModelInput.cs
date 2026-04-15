using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveCompetencyModelInput
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string ScaleType { get; set; } = "Numeric";

    public int MinScore { get; set; } = 1;
    public int MaxScore { get; set; } = 5;
}

using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Competencies;

public class CreateUpdateCompetencyDto
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
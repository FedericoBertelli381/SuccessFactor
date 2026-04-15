using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveCompetencyCatalogItemInput
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

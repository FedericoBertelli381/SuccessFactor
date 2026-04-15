using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveCompetencyModelItemInput
{
    [Required]
    public Guid ModelId { get; set; }

    [Required]
    public Guid CompetencyId { get; set; }

    [Range(0, 100)]
    public decimal? Weight { get; set; }

    public bool IsRequired { get; set; } = true;
}

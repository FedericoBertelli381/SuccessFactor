using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Cycles;

public class CreateUpdateCycleDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = default!;

    [Range(2000, 2100)]
    public int CycleYear { get; set; }

    [Required]
    public Guid TemplateId { get; set; }

    public Guid? CurrentPhaseId { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Draft"; // Draft|Active|Closed

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
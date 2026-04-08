using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Goals;

public class AddGoalProgressDto
{
    [Required] public Guid AssignmentId { get; set; }
    [Required] public DateOnly EntryDate { get; set; }

    [Range(0, 100)]
    public decimal? ProgressPercent { get; set; }

    public decimal? ActualValue { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    public Guid? AttachmentId { get; set; }
}
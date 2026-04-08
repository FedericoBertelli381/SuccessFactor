using System;

namespace SuccessFactor.Goals;

public class AddGoalProgressInput
{
    public Guid AssignmentId { get; set; }

    public DateOnly EntryDate { get; set; }

    public decimal? ProgressPercent { get; set; }

    public decimal? ActualValue { get; set; }

    public string? Note { get; set; }

    public Guid? AttachmentId { get; set; }
}
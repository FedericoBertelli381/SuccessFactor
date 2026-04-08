using System;

namespace SuccessFactor.Goals;

public class GoalProgressSummaryDto
{
    public Guid AssignmentId { get; set; }

    public int EntriesCount { get; set; }

    public DateOnly? LastEntryDate { get; set; }

    public decimal? LastProgressPercent { get; set; }

    public decimal? LastActualValue { get; set; }

    public string? LastNote { get; set; }
}
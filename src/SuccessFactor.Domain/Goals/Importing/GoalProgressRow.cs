using System;
using Volo.Abp.Domain.Entities;

namespace SuccessFactor.Goals.Importing;

public class GoalProgressRow : Entity<Guid>
{
    public Guid BatchId { get; set; }
    public int RowNumber { get; set; }

    public string RawJson { get; set; } = default!;
    public string ValidationStatus { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
}
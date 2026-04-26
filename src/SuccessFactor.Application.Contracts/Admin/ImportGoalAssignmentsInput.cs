using System;

namespace SuccessFactor.Admin;

public class ImportGoalAssignmentsInput
{
    public Guid CycleId { get; set; }
    public string? Content { get; set; }
    public bool UpdateExisting { get; set; }
}

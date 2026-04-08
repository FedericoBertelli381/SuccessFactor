using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Goals;

public class CreateUpdateGoalAssignmentDto
{
    [Required] public Guid CycleId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    [Required] public Guid GoalId { get; set; }

    [Range(0, 100)]
    public decimal Weight { get; set; }

    public decimal? TargetValue { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Draft";
}
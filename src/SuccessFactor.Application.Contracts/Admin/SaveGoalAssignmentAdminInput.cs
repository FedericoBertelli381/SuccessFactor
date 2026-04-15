using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveGoalAssignmentAdminInput
{
    [Required]
    public Guid CycleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid GoalId { get; set; }

    [Range(0, 100)]
    public decimal Weight { get; set; }

    public decimal? TargetValue { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Draft";
}

using System;

namespace SuccessFactor.Admin;

public class GoalAssignmentAdminListItemDto
{
    public Guid AssignmentId { get; set; }
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid GoalId { get; set; }
    public string GoalTitle { get; set; } = string.Empty;
    public string? GoalCategory { get; set; }
    public decimal Weight { get; set; }
    public decimal? TargetValue { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

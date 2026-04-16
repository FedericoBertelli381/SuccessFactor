using System;

namespace SuccessFactor.Team;

public class TeamReportGoalIssueDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid AssignmentId { get; set; }
    public string GoalTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
    public decimal? LastProgressPercent { get; set; }
    public DateOnly? LastProgressDate { get; set; }
    public string Issue { get; set; } = string.Empty;
}

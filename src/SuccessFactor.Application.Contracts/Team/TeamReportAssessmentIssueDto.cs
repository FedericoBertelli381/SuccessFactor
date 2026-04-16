using System;

namespace SuccessFactor.Team;

public class TeamReportAssessmentIssueDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid? AssessmentId { get; set; }
    public string AssessmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int RequiredItemsCount { get; set; }
    public int MissingRequiredCount { get; set; }
    public string Issue { get; set; } = string.Empty;
}

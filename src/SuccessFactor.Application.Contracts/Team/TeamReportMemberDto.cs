using System;

namespace SuccessFactor.Team;

public class TeamReportMemberDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string ParticipantStatus { get; set; } = string.Empty;
    public string CurrentPhaseCode { get; set; } = string.Empty;
    public string CurrentPhaseName { get; set; } = string.Empty;
    public int GoalAssignmentCount { get; set; }
    public int AssessmentCount { get; set; }
    public int MissingRequiredAssessmentCount { get; set; }
    public decimal? AverageLatestProgressPercent { get; set; }
}

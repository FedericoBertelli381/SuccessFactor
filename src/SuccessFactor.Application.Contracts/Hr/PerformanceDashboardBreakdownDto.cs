using System;

namespace SuccessFactor.Hr;

public class PerformanceDashboardBreakdownDto
{
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public int GoalAssignmentCount { get; set; }
    public decimal? AverageLatestProgressPercent { get; set; }
    public int AssessmentCount { get; set; }
    public decimal AssessmentCompletionPercent { get; set; }
    public decimal? AverageScore { get; set; }
}

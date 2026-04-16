using System;
using System.Collections.Generic;

namespace SuccessFactor.Hr;

public class PerformanceDashboardDto
{
    public Guid? SelectedCycleId { get; set; }
    public string? SelectedCycleName { get; set; }
    public string? SelectedCycleStatus { get; set; }

    public int ParticipantCount { get; set; }
    public int CompletedParticipantCount { get; set; }
    public decimal ParticipantCompletionPercent { get; set; }
    public int GoalAssignmentCount { get; set; }
    public decimal? AverageLatestProgressPercent { get; set; }
    public int AssessmentCount { get; set; }
    public int SubmittedOrClosedAssessmentCount { get; set; }
    public decimal AssessmentCompletionPercent { get; set; }
    public decimal? AverageScore { get; set; }

    public List<HrCycleLookupDto> Cycles { get; set; } = [];
    public List<PerformanceDashboardScoreBucketDto> ScoreDistribution { get; set; } = [];
    public List<PerformanceDashboardBreakdownDto> OrgUnitBreakdown { get; set; } = [];
    public List<PerformanceDashboardBreakdownDto> JobRoleBreakdown { get; set; } = [];
}

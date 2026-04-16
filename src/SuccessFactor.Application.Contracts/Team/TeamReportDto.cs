using System;
using System.Collections.Generic;

namespace SuccessFactor.Team;

public class TeamReportDto
{
    public Guid ManagerEmployeeId { get; set; }
    public string ManagerEmployeeName { get; set; } = string.Empty;
    public Guid? SelectedCycleId { get; set; }
    public string? SelectedCycleName { get; set; }
    public string? SelectedCycleStatus { get; set; }

    public int TeamMemberCount { get; set; }
    public int ParticipantsInCycleCount { get; set; }
    public int GoalAssignmentCount { get; set; }
    public int GoalsWithoutProgressCount { get; set; }
    public int OverdueGoalCount { get; set; }
    public int AssessmentCount { get; set; }
    public int AssessmentIssueCount { get; set; }
    public decimal? AverageLatestProgressPercent { get; set; }

    public List<TeamReportCycleLookupDto> Cycles { get; set; } = [];
    public List<TeamReportMemberDto> Members { get; set; } = [];
    public List<TeamReportPhaseDto> Phases { get; set; } = [];
    public List<TeamReportGoalStatusDto> GoalStatuses { get; set; } = [];
    public List<TeamReportGoalIssueDto> GoalIssues { get; set; } = [];
    public List<TeamReportAssessmentIssueDto> AssessmentIssues { get; set; } = [];
}

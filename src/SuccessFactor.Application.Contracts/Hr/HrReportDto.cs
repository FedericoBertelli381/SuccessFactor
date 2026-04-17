using System;
using System.Collections.Generic;

namespace SuccessFactor.Hr;

public class HrReportDto
{
    public Guid? SelectedCycleId { get; set; }
    public string? SelectedCycleName { get; set; }
    public string? SelectedCycleStatus { get; set; }

    public int TotalParticipants { get; set; }
    public int ActiveParticipants { get; set; }
    public int CompletedParticipants { get; set; }
    public int ExcludedParticipants { get; set; }
    public int TotalAssessments { get; set; }
    public int DraftAssessments { get; set; }
    public int SubmittedAssessments { get; set; }
    public int ClosedAssessments { get; set; }
    public int AssessmentsWithMissingRequired { get; set; }
    public int EmployeesWithoutManagerCount { get; set; }
    public int EmployeesWithoutUserLinkCount { get; set; }

    public List<HrCycleLookupDto> Cycles { get; set; } = [];
    public List<HrReportParticipantStatusDto> ParticipantStatuses { get; set; } = [];
    public List<HrReportPhaseDto> Phases { get; set; } = [];
    public List<HrReportAssessmentGroupDto> AssessmentStatuses { get; set; } = [];
    public List<HrReportAssessmentGroupDto> AssessmentTypes { get; set; } = [];
    public List<HrReportMissingRequiredAssessmentDto> MissingRequiredAssessments { get; set; } = [];
    public List<HrReportEmployeeIssueDto> EmployeesWithoutManager { get; set; } = [];
    public List<HrReportEmployeeIssueDto> EmployeesWithoutUserLink { get; set; } = [];
    public List<HrExportLookupDto> ExportOrgUnits { get; set; } = [];
    public List<HrExportLookupDto> ExportJobRoles { get; set; } = [];
}

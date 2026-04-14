using System;

namespace SuccessFactor.Admin;

public class CycleAdminListItemDto
{
    public Guid CycleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CycleYear { get; set; }
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public Guid? CurrentPhaseId { get; set; }
    public string? CurrentPhaseCode { get; set; }
    public string? CurrentPhaseName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int ParticipantCount { get; set; }
    public int GoalAssignmentCount { get; set; }
    public int AssessmentCount { get; set; }
    public int DraftAssessmentCount { get; set; }
    public bool HasSetupData { get; set; }
    public bool CanActivate { get; set; }
    public bool CanClose { get; set; }
}

using System;

namespace SuccessFactor.Competencies.Assessments;

public class CompetencyAssessmentItemDto
{
    public Guid CompetencyId { get; set; }
    public int? Score { get; set; }
    public string? Comment { get; set; }
    public Guid? EvidenceAttachmentId { get; set; }
}
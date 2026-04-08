using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Competencies.Assessments;

public class UpsertAssessmentItemDto
{
    [Required] public Guid AssessmentId { get; set; }
    [Required] public Guid CompetencyId { get; set; }

    public int? Score { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }

    public Guid? EvidenceAttachmentId { get; set; }
}
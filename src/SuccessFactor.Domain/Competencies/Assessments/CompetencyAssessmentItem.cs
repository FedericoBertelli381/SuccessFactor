using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Competencies.Assessments;

public class CompetencyAssessmentItem : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid AssessmentId { get; set; }
    public Guid CompetencyId { get; set; }

    public int? Score { get; set; }
    public string? Comment { get; set; }
    public Guid? EvidenceAttachmentId { get; set; }
}
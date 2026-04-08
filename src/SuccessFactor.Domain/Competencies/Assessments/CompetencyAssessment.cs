using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Competencies.Assessments;

public class CompetencyAssessment : Entity<Guid>, IMultiTenant,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid? TenantId { get; set; }

    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid EvaluatorEmployeeId { get; set; }

    public Guid? ModelId { get; set; } // <-- se hai applicato ALTER TABLE
    public string AssessmentType { get; set; } = "Manager"; // Self|Manager|Peer|HR|...
    public string Status { get; set; } = "Draft"; // Draft|Submitted|Closed

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
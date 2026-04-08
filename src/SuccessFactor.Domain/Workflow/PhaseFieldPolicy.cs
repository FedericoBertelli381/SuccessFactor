using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace SuccessFactor.Workflow;

public class PhaseFieldPolicy : Entity<Guid>,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid TemplateId { get; set; }
    public Guid PhaseId { get; set; }

    public string FieldKey { get; set; } = default!;
    public string RoleCode { get; set; } = "*";    // * = tutti

    public string Access { get; set; } = "Read";   // Hidden|Read|Edit
    public bool IsRequired { get; set; }

    public string? ConditionExpr { get; set; }

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
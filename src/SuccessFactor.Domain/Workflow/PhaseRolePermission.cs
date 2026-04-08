using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace SuccessFactor.Workflow;

public class PhaseRolePermission : Entity<Guid>,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid TemplateId { get; set; }
    public Guid PhaseId { get; set; }

    public string RoleCode { get; set; } = "*";

    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanAdvance { get; set; }

    public string? ConditionExpr { get; set; }

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
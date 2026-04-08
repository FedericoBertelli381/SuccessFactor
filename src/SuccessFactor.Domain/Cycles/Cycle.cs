using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Cycles;

public class Cycle : Entity<Guid>, IMultiTenant,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = default!;
    public int CycleYear { get; set; }

    public Guid TemplateId { get; set; }       // FK -> ProcessTemplates
    public Guid? CurrentPhaseId { get; set; }  // lo useremo dopo (ProcessPhases)

    public string Status { get; set; } = "Draft"; // Draft|Active|Closed
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
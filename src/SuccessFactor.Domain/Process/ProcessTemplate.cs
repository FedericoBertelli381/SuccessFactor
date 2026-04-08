using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Process;

public class ProcessTemplate : Entity<Guid>, IMultiTenant,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = default!;
    public int Version { get; set; }
    public bool IsDefault { get; set; }

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
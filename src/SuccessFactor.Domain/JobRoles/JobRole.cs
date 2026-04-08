using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.JobRoles;

public class JobRole : Entity<Guid>, IMultiTenant,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = default!;

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
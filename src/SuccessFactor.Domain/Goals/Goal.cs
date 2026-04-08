using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Goals;

public class Goal : Entity<Guid>, IMultiTenant, ICreationAuditedObject, IModificationAuditedObject
{
    // Multi-tenant
    public Guid? TenantId { get; set; }

    // Campi dominio (tabella dbo.Goals)
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsLibraryItem { get; set; }
    public decimal? DefaultWeight { get; set; }

    // Auditing (mappiamo su CreatedAt / CreatedByUserId / ModifiedAt / ModifiedByUserId)
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    // Concurrency (mappiamo su RowVer rowversion)
    public byte[] RowVer { get; set; } = default!;
}
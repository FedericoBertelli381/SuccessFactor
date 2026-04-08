using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Goals.Importing;

public class GoalProgressBatch : Entity<Guid>, IMultiTenant, ICreationAuditedObject
{
    public Guid? TenantId { get; set; }

    public Guid CycleId { get; set; }
    public string FileName { get; set; } = default!;
    public string Status { get; set; } = "Uploaded"; // Uploaded|Validated|Committed|Failed

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
}
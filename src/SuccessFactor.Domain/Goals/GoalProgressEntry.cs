using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Goals;

public class GoalProgressEntry : Entity<Guid>, IMultiTenant, ICreationAuditedObject
{
    public Guid? TenantId { get; set; }

    public Guid AssignmentId { get; set; }
    public DateOnly EntryDate { get; set; }

    public decimal? ProgressPercent { get; set; }   // 0..100
    public decimal? ActualValue { get; set; }
    public string? Note { get; set; }
    public Guid? AttachmentId { get; set; }

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
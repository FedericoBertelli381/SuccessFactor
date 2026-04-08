using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Goals;

public class GoalAssignment : Entity<Guid>, IMultiTenant,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid? TenantId { get; set; }

    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid GoalId { get; set; }

    public decimal Weight { get; set; }
    public decimal? TargetValue { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    public string Status { get; set; } = "Draft"; // Draft|Approved|InProgress|Closed

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
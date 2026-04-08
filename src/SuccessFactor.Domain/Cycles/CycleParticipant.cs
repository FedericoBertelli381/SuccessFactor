using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Cycles;

public class CycleParticipant : Entity<Guid>, IMultiTenant, ICreationAuditedObject
{
    public Guid? TenantId { get; set; }

    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }

    public Guid? CurrentPhaseId { get; set; }
    public string Status { get; set; } = "Active"; // Active|Completed|Excluded

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
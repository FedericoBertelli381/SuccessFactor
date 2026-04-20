using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Auditing;

public class BusinessAuditEvent : Entity<Guid>, IMultiTenant
{
    private BusinessAuditEvent()
    {
    }

    public BusinessAuditEvent(Guid id)
        : base(id)
    {
    }

    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTime EventTime { get; set; }
    public string? Payload { get; set; }
}

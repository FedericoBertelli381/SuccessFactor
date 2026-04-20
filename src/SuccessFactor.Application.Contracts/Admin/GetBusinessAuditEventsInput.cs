using System;

namespace SuccessFactor.Admin;

public class GetBusinessAuditEventsInput
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? User { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public Guid? TenantId { get; set; }
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 50;
}

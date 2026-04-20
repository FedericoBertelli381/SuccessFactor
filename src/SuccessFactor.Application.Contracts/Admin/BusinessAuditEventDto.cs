using System;

namespace SuccessFactor.Admin;

public class BusinessAuditEventDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTime EventTime { get; set; }
    public string? Payload { get; set; }
}

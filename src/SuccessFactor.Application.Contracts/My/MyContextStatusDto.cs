using System;

namespace SuccessFactor.My;

public class MyContextStatusDto
{
    public bool IsReady { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}

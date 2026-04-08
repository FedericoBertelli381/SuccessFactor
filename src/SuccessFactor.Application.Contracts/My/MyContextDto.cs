using System;

namespace SuccessFactor.My;

public class MyContextDto
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = default!;
    public string Matricola { get; set; } = default!;
    public string? Email { get; set; }

    public Guid? OrgUnitId { get; set; }
    public Guid? JobRoleId { get; set; }

    public string[] AbpRoles { get; set; } = Array.Empty<string>();
    public string[] RoleCodes { get; set; } = Array.Empty<string>(); // HR|Manager|Employee
}
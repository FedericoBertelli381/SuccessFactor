using System;

namespace SuccessFactor.Admin;

public class EmployeeAdminListItemDto
{
    public Guid EmployeeId { get; set; }
    public Guid? UserId { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
    public Guid? JobRoleId { get; set; }
    public string? JobRoleName { get; set; }
    public bool IsActive { get; set; }
}

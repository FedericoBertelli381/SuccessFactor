using System;

namespace SuccessFactor.Admin;

public class JobRoleAdminListItemDto
{
    public Guid JobRoleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public bool CanDelete { get; set; }
}

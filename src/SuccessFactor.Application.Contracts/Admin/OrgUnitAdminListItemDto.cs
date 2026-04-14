using System;

namespace SuccessFactor.Admin;

public class OrgUnitAdminListItemDto
{
    public Guid OrgUnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentOrgUnitId { get; set; }
    public string? ParentOrgUnitName { get; set; }
    public int Level { get; set; }
    public int ChildCount { get; set; }
    public int EmployeeCount { get; set; }
    public bool CanDelete { get; set; }
}

using System;

namespace SuccessFactor.Admin;

public class ManagerRelationAdminListItemDto
{
    public Guid RelationId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeMatricola { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid ManagerEmployeeId { get; set; }
    public string ManagerMatricola { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }
}

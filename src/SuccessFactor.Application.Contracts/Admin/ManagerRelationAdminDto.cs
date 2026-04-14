using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class ManagerRelationAdminDto
{
    public List<EmployeeAdminListItemDto> Employees { get; set; } = [];
    public List<ManagerRelationAdminListItemDto> Relations { get; set; } = [];
}

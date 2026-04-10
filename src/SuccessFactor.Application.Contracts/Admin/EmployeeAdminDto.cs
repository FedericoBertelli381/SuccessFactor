using System;
using System.Collections.Generic;
using SuccessFactor.Employees;

namespace SuccessFactor.Admin;

public class EmployeeAdminDto
{
    public List<EmployeeAdminListItemDto> Employees { get; set; } = [];
    public List<AdminLookupDto> OrgUnits { get; set; } = [];
    public List<AdminLookupDto> JobRoles { get; set; } = [];
    public CreateUpdateEmployeeDto NewEmployeeDefaults { get; set; } = new();
}

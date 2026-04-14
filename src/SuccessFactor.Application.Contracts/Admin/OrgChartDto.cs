using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class OrgChartDto
{
    public List<OrgChartOrgUnitDto> OrgUnits { get; set; } = [];
    public List<OrgChartEmployeeDto> EmployeesWithoutOrgUnit { get; set; } = [];
    public int TotalOrgUnits { get; set; }
    public int TotalEmployees { get; set; }
    public int EmployeesWithoutOrgUnitCount { get; set; }
}

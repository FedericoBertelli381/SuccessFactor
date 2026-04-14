using System;
using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class OrgChartOrgUnitDto
{
    public Guid OrgUnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentOrgUnitId { get; set; }
    public string? ParentOrgUnitName { get; set; }
    public int Level { get; set; }
    public string Path { get; set; } = string.Empty;
    public int DirectEmployeeCount { get; set; }
    public int ChildOrgUnitCount { get; set; }
    public List<OrgChartEmployeeDto> Employees { get; set; } = [];
}

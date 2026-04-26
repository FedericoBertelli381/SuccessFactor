using System.Collections.Generic;

namespace SuccessFactor.Employees.IdentityLink;

public class EmployeeUserLinkImportResultDto
{
    public bool HasErrors { get; set; }
    public int LinkedCount { get; set; }
    public int RelinkedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<EmployeeUserLinkImportRowResultDto> Rows { get; set; } = [];
}

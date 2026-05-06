using System.Collections.Generic;

namespace SuccessFactor.Employees.IdentityLink;

public class IdentityUserImportResultDto
{
    public bool HasErrors { get; set; }
    public int CreatedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<IdentityUserImportRowResultDto> Rows { get; set; } = [];
}

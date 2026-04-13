using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class EmployeeImportResultDto
{
    public bool HasErrors { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<EmployeeImportRowResultDto> Rows { get; set; } = [];
}

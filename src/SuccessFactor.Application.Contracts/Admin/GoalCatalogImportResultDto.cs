using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class GoalCatalogImportResultDto
{
    public bool HasErrors { get; set; }
    public int ErrorCount { get; set; }
    public int CreatedGoals { get; set; }
    public int UpdatedGoals { get; set; }
    public List<GoalCatalogImportRowResultDto> Rows { get; set; } = [];
}

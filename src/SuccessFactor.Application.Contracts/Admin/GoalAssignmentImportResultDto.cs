using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class GoalAssignmentImportResultDto
{
    public bool HasErrors { get; set; }
    public int ErrorCount { get; set; }
    public int CreatedAssignments { get; set; }
    public int UpdatedAssignments { get; set; }
    public List<GoalAssignmentImportRowResultDto> Rows { get; set; } = [];
}

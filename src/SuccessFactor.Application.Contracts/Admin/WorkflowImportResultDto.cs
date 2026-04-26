using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class WorkflowImportResultDto
{
    public bool HasErrors { get; set; }
    public int ErrorCount { get; set; }
    public int CreatedTemplates { get; set; }
    public int UpdatedTemplates { get; set; }
    public int CreatedPhases { get; set; }
    public int UpdatedPhases { get; set; }
    public int CreatedTransitions { get; set; }
    public int UpdatedTransitions { get; set; }
    public int CreatedRolePermissions { get; set; }
    public int UpdatedRolePermissions { get; set; }
    public int CreatedFieldPolicies { get; set; }
    public int UpdatedFieldPolicies { get; set; }
    public List<WorkflowImportRowResultDto> Rows { get; set; } = [];
}

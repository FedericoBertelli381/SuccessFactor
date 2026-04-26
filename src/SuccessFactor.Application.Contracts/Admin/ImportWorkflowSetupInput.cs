namespace SuccessFactor.Admin;

public class ImportWorkflowSetupInput
{
    public string? TemplatesContent { get; set; }
    public string? PhasesContent { get; set; }
    public string? TransitionsContent { get; set; }
    public string? RolePermissionsContent { get; set; }
    public string? FieldPoliciesContent { get; set; }
    public bool UpdateExisting { get; set; }
}

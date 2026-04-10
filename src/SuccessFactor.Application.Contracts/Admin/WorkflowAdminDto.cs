using System;
using System.Collections.Generic;
using SuccessFactor.Workflow;

namespace SuccessFactor.Admin;

public class WorkflowAdminDto
{
    public Guid? SelectedTemplateId { get; set; }
    public Guid? SelectedPhaseId { get; set; }
    public string? SelectedTemplateName { get; set; }
    public string? SelectedPhaseCode { get; set; }
    public string? SelectedPhaseName { get; set; }

    public List<WorkflowTemplateLookupDto> Templates { get; set; } = [];
    public List<WorkflowPhaseLookupDto> Phases { get; set; } = [];
    public List<PhaseRolePermissionDto> RolePermissions { get; set; } = [];
    public List<PhaseFieldPolicyDto> FieldPolicies { get; set; } = [];
}

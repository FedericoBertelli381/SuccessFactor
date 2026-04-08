using System;
using System.Collections.Generic;

namespace SuccessFactor.Workflow;

public class WorkflowAccessContextDto
{
    public Guid CycleId { get; set; }
    public Guid TargetEmployeeId { get; set; }

    public Guid TemplateId { get; set; }
    public Guid PhaseId { get; set; }

    public Guid ActorEmployeeId { get; set; }
    public string RoleCodeUsed { get; set; } = default!; // HR|Manager|Employee|*

    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanAdvance { get; set; }

    // policies già definite da te (PhaseFieldPolicyDto)
    public List<PhaseFieldPolicyDto> FieldPolicies { get; set; } = new();
}
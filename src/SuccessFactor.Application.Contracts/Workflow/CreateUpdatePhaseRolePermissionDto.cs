using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Workflow;

public class CreateUpdatePhaseRolePermissionDto
{
    [Required] public Guid TemplateId { get; set; }
    [Required] public Guid PhaseId { get; set; }

    [Required, StringLength(50)] public string RoleCode { get; set; } = "*";

    public bool CanView { get; set; } = true;
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanAdvance { get; set; }

    [StringLength(2000)] public string? ConditionExpr { get; set; }
}
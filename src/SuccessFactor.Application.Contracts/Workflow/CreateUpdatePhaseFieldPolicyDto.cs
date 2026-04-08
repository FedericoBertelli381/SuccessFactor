using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Workflow;

public class CreateUpdatePhaseFieldPolicyDto
{
    [Required] public Guid TemplateId { get; set; }
    [Required] public Guid PhaseId { get; set; }

    [Required, StringLength(100)] public string FieldKey { get; set; } = default!;
    [Required, StringLength(50)] public string RoleCode { get; set; } = "*";

    [Required, StringLength(20)] public string Access { get; set; } = "Read"; // Hidden|Read|Edit
    public bool IsRequired { get; set; }

    [StringLength(2000)] public string? ConditionExpr { get; set; }
}
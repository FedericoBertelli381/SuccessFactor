using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Workflow;

public class CreateUpdatePhaseTransitionDto
{
    [Required] public Guid TemplateId { get; set; }
    [Required] public Guid FromPhaseId { get; set; }
    [Required] public Guid ToPhaseId { get; set; }
    [StringLength(2000)] public string? ConditionExpr { get; set; }
}
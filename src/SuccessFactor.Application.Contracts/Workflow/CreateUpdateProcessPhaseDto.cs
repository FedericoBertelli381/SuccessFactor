using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Workflow;

public class CreateUpdateProcessPhaseDto
{
    [Required] public Guid TemplateId { get; set; }

    [Required, StringLength(50)] public string Code { get; set; } = default!;
    [Required, StringLength(200)] public string Name { get; set; } = default!;

    public int PhaseOrder { get; set; }
    public bool IsTerminal { get; set; }

    [StringLength(2000)] public string? StartRule { get; set; }
    [StringLength(2000)] public string? EndRule { get; set; }
}
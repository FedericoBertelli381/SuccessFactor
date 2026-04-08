using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Workflow;

public class ProcessPhaseDto : EntityDto<Guid>
{
    public Guid TemplateId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int PhaseOrder { get; set; }
    public bool IsTerminal { get; set; }
    public string? StartRule { get; set; }
    public string? EndRule { get; set; }
}
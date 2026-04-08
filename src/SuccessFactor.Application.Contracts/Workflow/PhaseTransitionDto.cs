using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Workflow;

public class PhaseTransitionDto : EntityDto<Guid>
{
    public Guid TemplateId { get; set; }
    public Guid FromPhaseId { get; set; }
    public Guid ToPhaseId { get; set; }
    public string? ConditionExpr { get; set; }
}
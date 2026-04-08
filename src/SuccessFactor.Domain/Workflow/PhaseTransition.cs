using System;
using Volo.Abp.Domain.Entities;

namespace SuccessFactor.Workflow;

public class PhaseTransition : Entity<Guid>
{
    public Guid TemplateId { get; set; }
    public Guid FromPhaseId { get; set; }
    public Guid ToPhaseId { get; set; }

    public string? ConditionExpr { get; set; }
}
using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Workflow;

public class PhaseFieldPolicyDto : EntityDto<Guid>
{
    public Guid TemplateId { get; set; }
    public Guid PhaseId { get; set; }

    public string FieldKey { get; set; } = default!;
    public string RoleCode { get; set; } = default!;

    public string Access { get; set; } = default!;
    public bool IsRequired { get; set; }
    public string? ConditionExpr { get; set; }
}
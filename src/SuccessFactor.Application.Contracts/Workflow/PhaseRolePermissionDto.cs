using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Workflow;

public class PhaseRolePermissionDto : EntityDto<Guid>
{
    public Guid TemplateId { get; set; }
    public Guid PhaseId { get; set; }
    public string RoleCode { get; set; } = default!;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanAdvance { get; set; }
    public string? ConditionExpr { get; set; }
}
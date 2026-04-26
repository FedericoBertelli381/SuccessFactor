using System;
using System.Threading.Tasks;
using SuccessFactor.Process;
using SuccessFactor.Workflow;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IWorkflowAdminAppService : IApplicationService
{
    Task<WorkflowAdminDto> GetAsync(GetWorkflowAdminInput input);
    Task<WorkflowImportResultDto> ImportAsync(ImportWorkflowSetupInput input);
    Task<WorkflowTemplateLookupDto> SaveTemplateAsync(Guid? id, CreateUpdateProcessTemplateDto input);
    Task DeleteTemplateAsync(Guid id);
    Task<WorkflowPhaseLookupDto> SavePhaseAsync(Guid? id, CreateUpdateProcessPhaseDto input);
    Task DeletePhaseAsync(Guid id);
    Task<WorkflowTransitionAdminDto> SaveTransitionAsync(Guid? id, CreateUpdatePhaseTransitionDto input);
    Task DeleteTransitionAsync(Guid id);
    Task<PhaseRolePermissionDto> SaveRolePermissionAsync(Guid? id, CreateUpdatePhaseRolePermissionDto input);
    Task DeleteRolePermissionAsync(Guid id);
    Task<PhaseFieldPolicyDto> SaveFieldPolicyAsync(Guid? id, CreateUpdatePhaseFieldPolicyDto input);
    Task DeleteFieldPolicyAsync(Guid id);
}

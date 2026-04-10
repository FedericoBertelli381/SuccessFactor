using System;
using System.Threading.Tasks;
using SuccessFactor.Workflow;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IWorkflowAdminAppService : IApplicationService
{
    Task<WorkflowAdminDto> GetAsync(GetWorkflowAdminInput input);
    Task<PhaseRolePermissionDto> SaveRolePermissionAsync(Guid? id, CreateUpdatePhaseRolePermissionDto input);
    Task DeleteRolePermissionAsync(Guid id);
    Task<PhaseFieldPolicyDto> SaveFieldPolicyAsync(Guid? id, CreateUpdatePhaseFieldPolicyDto input);
    Task DeleteFieldPolicyAsync(Guid id);
}

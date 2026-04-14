using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminJobRoleAppService : IApplicationService
{
    Task<JobRoleAdminDto> GetAsync();
    Task<JobRoleAdminListItemDto> SaveAsync(Guid? jobRoleId, SaveJobRoleInput input);
    Task DeleteAsync(Guid jobRoleId);
}

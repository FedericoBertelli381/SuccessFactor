using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminOrgUnitAppService : IApplicationService
{
    Task<OrgUnitAdminDto> GetAsync();
    Task<OrgUnitAdminListItemDto> SaveAsync(Guid? orgUnitId, SaveOrgUnitInput input);
    Task DeleteAsync(Guid orgUnitId);
}

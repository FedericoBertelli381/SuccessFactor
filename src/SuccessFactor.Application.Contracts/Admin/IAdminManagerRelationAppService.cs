using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminManagerRelationAppService : IApplicationService
{
    Task<ManagerRelationAdminDto> GetAsync();
    Task<ManagerRelationAdminListItemDto> SaveAsync(Guid? relationId, SaveManagerRelationInput input);
    Task EndAsync(Guid relationId, DateOnly? endDate = null);
    Task DeleteAsync(Guid relationId);
}

using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminGoalCatalogAppService : IApplicationService
{
    Task<GoalCatalogAdminDto> GetAsync();
    Task<GoalCatalogAdminListItemDto> SaveAsync(Guid? goalId, SaveGoalCatalogItemInput input);
    Task DeleteAsync(Guid goalId);
}

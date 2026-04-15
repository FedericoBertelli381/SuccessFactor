using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminCompetencyModelAppService : IApplicationService
{
    Task<CompetencyModelAdminDto> GetAsync(Guid? modelId = null);
    Task<CompetencyModelAdminListItemDto> SaveModelAsync(Guid? modelId, SaveCompetencyModelInput input);
    Task DeleteModelAsync(Guid modelId);
    Task<CompetencyModelItemAdminListItemDto> SaveItemAsync(Guid? modelItemId, SaveCompetencyModelItemInput input);
    Task DeleteItemAsync(Guid modelItemId);
}

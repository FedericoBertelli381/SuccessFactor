using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminCompetencyCatalogAppService : IApplicationService
{
    Task<CompetencyCatalogAdminDto> GetAsync();
    Task<CompetencyCatalogAdminListItemDto> SaveAsync(Guid? competencyId, SaveCompetencyCatalogItemInput input);
    Task DeleteAsync(Guid competencyId);
}

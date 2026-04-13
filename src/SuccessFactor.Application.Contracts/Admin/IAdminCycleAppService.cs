using System;
using System.Threading.Tasks;
using SuccessFactor.Cycles;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminCycleAppService : IApplicationService
{
    Task<CycleAdminDto> GetAsync(Guid? templateId = null);
    Task<CycleAdminListItemDto> SaveAsync(Guid? id, CreateUpdateCycleDto input);
}

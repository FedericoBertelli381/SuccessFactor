using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminCycleParticipantAppService : IApplicationService
{
    Task<CycleParticipantAdminDto> GetAsync(Guid? cycleId = null);
    Task<CycleParticipantAdminListItemDto> SaveAsync(Guid? participantId, SaveCycleParticipantInput input);
    Task DeleteAsync(Guid participantId);
}

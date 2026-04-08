using System;
using System.Threading.Tasks;
using SuccessFactor.My.Dtos;
using Volo.Abp.Application.Services;

namespace SuccessFactor.My;

public interface IMyDashboardAppService : IApplicationService
{
    Task<MyDashboardDto> GetAsync(GetMyDashboardInput input);
    Task AdvancePhaseAsync(Guid cycleId);
}
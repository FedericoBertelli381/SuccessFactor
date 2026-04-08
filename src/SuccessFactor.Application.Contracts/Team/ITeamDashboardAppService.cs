using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Team;

public interface ITeamDashboardAppService : IApplicationService
{
    Task<TeamDashboardDto> GetAsync(GetTeamDashboardInput input);
    Task AdvancePhaseAsync(Guid targetEmployeeId, Guid? cycleId);
}
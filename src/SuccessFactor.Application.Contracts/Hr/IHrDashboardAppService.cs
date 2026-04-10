using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Hr;

public interface IHrDashboardAppService : IApplicationService
{
    Task<HrDashboardDto> GetAsync(GetHrDashboardInput input);
}

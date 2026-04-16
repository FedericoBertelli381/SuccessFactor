using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Hr;

public interface IPerformanceDashboardAppService : IApplicationService
{
    Task<PerformanceDashboardDto> GetAsync(GetPerformanceDashboardInput input);
}

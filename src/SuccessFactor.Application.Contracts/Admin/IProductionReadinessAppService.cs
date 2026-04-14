using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IProductionReadinessAppService : IApplicationService
{
    Task<ProductionReadinessDto> GetAsync();
}

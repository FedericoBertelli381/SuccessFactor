using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IPerformanceSetupImportAppService : IApplicationService
{
    Task<PerformanceSetupImportResultDto> ImportAsync(ImportPerformanceSetupInput input);
}

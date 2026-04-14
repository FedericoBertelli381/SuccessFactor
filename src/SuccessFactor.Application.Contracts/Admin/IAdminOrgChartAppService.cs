using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminOrgChartAppService : IApplicationService
{
    Task<OrgChartDto> GetAsync();
}

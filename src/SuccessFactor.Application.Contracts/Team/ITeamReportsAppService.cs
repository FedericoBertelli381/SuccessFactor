using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Team;

public interface ITeamReportsAppService : IApplicationService
{
    Task<TeamReportDto> GetAsync(GetTeamReportInput input);
}

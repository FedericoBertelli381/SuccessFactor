using System.Threading.Tasks;
using SuccessFactor.My.Dtos;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Team;

public interface ITeamGoalsAppService : IApplicationService
{
    Task<MyGoalsDto> GetAsync(GetTeamGoalsInput input);
}
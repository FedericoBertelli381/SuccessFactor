using System.Threading.Tasks;
using SuccessFactor.My.Dtos;
using Volo.Abp.Application.Services;

namespace SuccessFactor.My;

public interface IMyGoalsAppService : IApplicationService
{
    Task<MyGoalsDto> GetAsync(GetMyGoalsInput input);
}
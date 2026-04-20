using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.My;

public interface IMyContextAppService : IApplicationService
{
    Task<MyContextDto> GetAsync(DateOnly? asOfDate = null);

    Task<MyContextStatusDto> GetStatusAsync();
}

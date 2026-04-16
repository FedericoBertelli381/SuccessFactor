using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminAssessmentSetupAppService : IApplicationService
{
    Task<AssessmentSetupAdminDto> GetAsync(Guid? cycleId = null);
    Task<AssessmentSetupAdminListItemDto> GenerateAsync(GenerateAssessmentSetupInput input);
}

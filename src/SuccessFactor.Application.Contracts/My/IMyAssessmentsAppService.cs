using SuccessFactor.Competencies.Assessments;
using SuccessFactor.My.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.My;

public interface IMyAssessmentsAppService : IApplicationService
{
    Task<MyAssessmentsDto> GetAsync(GetMyAssessmentsInput input);
    Task UpsertItemAsync(UpsertAssessmentItemDto input);

    Task SubmitAsync(Guid assessmentId);
}
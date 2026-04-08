using SuccessFactor.Competencies.Assessments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Competencies;

public interface ICompetencyAssessmentAppService : IApplicationService
{
    Task<CompetencyAssessmentItemDto> UpsertItemAsync(UpsertAssessmentItemDto input);

    Task SubmitAsync(Guid assessmentId);
}
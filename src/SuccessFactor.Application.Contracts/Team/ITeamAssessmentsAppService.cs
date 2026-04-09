using SuccessFactor.My.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Team;

public interface ITeamAssessmentsAppService : IApplicationService
{
    Task<MyAssessmentsDto> GetAsync(GetTeamAssessmentsInput input);
    Task SubmitAsync(Guid targetEmployeeId, Guid assessmentId, Guid? cycleId);
    Task UpdateItemAsync(Guid targetEmployeeId, Guid assessmentId, UpdateTeamAssessmentItemDto input, Guid? cycleId);
    Task<int> UpdateItemsAsync(Guid targetEmployeeId, Guid assessmentId, BulkUpdateTeamAssessmentItemsDto input, Guid? cycleId);
}

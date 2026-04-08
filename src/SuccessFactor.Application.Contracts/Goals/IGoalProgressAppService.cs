using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Goals;

public interface IGoalProgressAppService : IApplicationService
{
    Task<List<GoalProgressEntryDto>> GetByAssignmentAsync(Guid assignmentId);

    Task<GoalProgressEntryDto?> GetLastProgressAsync(Guid assignmentId);

    Task<GoalProgressSummaryDto> GetProgressSummaryAsync(Guid assignmentId);

    Task<GoalProgressEntryDto> AddAsync(AddGoalProgressDto input);
}
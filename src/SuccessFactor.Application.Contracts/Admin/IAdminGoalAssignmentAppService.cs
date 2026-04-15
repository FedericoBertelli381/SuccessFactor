using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SuccessFactor.Admin;

public interface IAdminGoalAssignmentAppService : IApplicationService
{
    Task<GoalAssignmentAdminDto> GetAsync(Guid? cycleId = null);
    Task<GoalAssignmentAdminListItemDto> SaveAsync(Guid? assignmentId, SaveGoalAssignmentAdminInput input);
    Task DeleteAsync(Guid assignmentId);
}

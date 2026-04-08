using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.Goals;

public class GoalAppService
    : CrudAppService<Goal, GoalDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateGoalDto>
{
    public GoalAppService(IRepository<Goal, Guid> repository)
        : base(repository)
    {
    }
}
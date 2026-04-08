using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.JobRoles;

public class JobRoleAppService :
    CrudAppService<JobRole, JobRoleDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateJobRoleDto>
{
    public JobRoleAppService(IRepository<JobRole, Guid> repository)
        : base(repository)
    {
    }
}
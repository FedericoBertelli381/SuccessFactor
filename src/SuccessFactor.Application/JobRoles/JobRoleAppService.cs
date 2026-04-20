using System;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Security;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.JobRoles;

[Authorize(Roles = SuccessFactorRoles.Admin)]

public class JobRoleAppService :
    CrudAppService<JobRole, JobRoleDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateJobRoleDto>
{
    public JobRoleAppService(IRepository<JobRole, Guid> repository)
        : base(repository)
    {
    }
}
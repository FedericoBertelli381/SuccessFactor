using System;
using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Security;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SuccessFactor.OrgUnits;

[Authorize(Roles = SuccessFactorRoles.Admin)]

public class OrgUnitAppService :
    CrudAppService<OrgUnit, OrgUnitDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateOrgUnitDto>
{
    public OrgUnitAppService(IRepository<OrgUnit, Guid> repository)
        : base(repository)
    {
    }
}
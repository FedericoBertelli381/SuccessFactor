using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.JobRoles;

public class JobRoleDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;
}
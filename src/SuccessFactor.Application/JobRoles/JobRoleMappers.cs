using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.JobRoles;

[Mapper]
public partial class JobRoleToJobRoleDtoMapper : TwoWayMapperBase<JobRole, JobRoleDto>
{
    public override partial JobRoleDto Map(JobRole source);
    public override partial void Map(JobRole source, JobRoleDto destination);

    public override partial JobRole ReverseMap(JobRoleDto destination);
    public override partial void ReverseMap(JobRoleDto destination, JobRole source);
}

[Mapper]
public partial class CreateUpdateJobRoleDtoToJobRoleMapper : MapperBase<CreateUpdateJobRoleDto, JobRole>
{
    public override partial JobRole Map(CreateUpdateJobRoleDto source);
    public override partial void Map(CreateUpdateJobRoleDto source, JobRole destination);
}
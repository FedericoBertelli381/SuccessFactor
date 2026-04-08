using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Workflow;

[Mapper]
public partial class PhaseRolePermissionMappers : TwoWayMapperBase<PhaseRolePermission, PhaseRolePermissionDto>
{
    public override partial PhaseRolePermissionDto Map(PhaseRolePermission source);
    public override partial void Map(PhaseRolePermission source, PhaseRolePermissionDto destination);
    public override partial PhaseRolePermission ReverseMap(PhaseRolePermissionDto destination);
    public override partial void ReverseMap(PhaseRolePermissionDto destination, PhaseRolePermission source);
}

[Mapper]
public partial class CreateUpdatePhaseRolePermissionMapper : MapperBase<CreateUpdatePhaseRolePermissionDto, PhaseRolePermission>
{
    public override partial PhaseRolePermission Map(CreateUpdatePhaseRolePermissionDto source);
    public override partial void Map(CreateUpdatePhaseRolePermissionDto source, PhaseRolePermission destination);
}

[Mapper]
public partial class PhaseFieldPolicyMappers : TwoWayMapperBase<PhaseFieldPolicy, PhaseFieldPolicyDto>
{
    public override partial PhaseFieldPolicyDto Map(PhaseFieldPolicy source);
    public override partial void Map(PhaseFieldPolicy source, PhaseFieldPolicyDto destination);
    public override partial PhaseFieldPolicy ReverseMap(PhaseFieldPolicyDto destination);
    public override partial void ReverseMap(PhaseFieldPolicyDto destination, PhaseFieldPolicy source);
}

[Mapper]
public partial class CreateUpdatePhaseFieldPolicyMapper : MapperBase<CreateUpdatePhaseFieldPolicyDto, PhaseFieldPolicy>
{
    public override partial PhaseFieldPolicy Map(CreateUpdatePhaseFieldPolicyDto source);
    public override partial void Map(CreateUpdatePhaseFieldPolicyDto source, PhaseFieldPolicy destination);
}
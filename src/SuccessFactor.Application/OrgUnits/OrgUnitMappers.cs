using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.OrgUnits;

[Mapper]
public partial class OrgUnitToOrgUnitDtoMapper : TwoWayMapperBase<OrgUnit, OrgUnitDto>
{
    public override partial OrgUnitDto Map(OrgUnit source);
    public override partial void Map(OrgUnit source, OrgUnitDto destination);

    public override partial OrgUnit ReverseMap(OrgUnitDto destination);
    public override partial void ReverseMap(OrgUnitDto destination, OrgUnit source);
}

[Mapper]
public partial class CreateUpdateOrgUnitDtoToOrgUnitMapper : MapperBase<CreateUpdateOrgUnitDto, OrgUnit>
{
    public override partial OrgUnit Map(CreateUpdateOrgUnitDto source);
    public override partial void Map(CreateUpdateOrgUnitDto source, OrgUnit destination);
}
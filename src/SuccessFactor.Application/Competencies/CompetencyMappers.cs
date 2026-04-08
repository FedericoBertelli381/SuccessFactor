using System;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using SuccessFactor.Competencies;

namespace SuccessFactor.Competencies;

[Mapper]
public partial class CompetencyToCompetencyDtoMapper : TwoWayMapperBase<Competency, CompetencyDto>
{
    public override partial CompetencyDto Map(Competency source);
    public override partial void Map(Competency source, CompetencyDto destination);

    public override partial Competency ReverseMap(CompetencyDto destination);
    public override partial void ReverseMap(CompetencyDto destination, Competency source);
}

[Mapper]
public partial class CreateUpdateCompetencyDtoToCompetencyMapper : MapperBase<CreateUpdateCompetencyDto, Competency>
{
    public override partial Competency Map(CreateUpdateCompetencyDto source);
    public override partial void Map(CreateUpdateCompetencyDto source, Competency destination);
}
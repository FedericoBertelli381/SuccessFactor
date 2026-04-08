using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Competencies.Models;

[Mapper]
public partial class CompetencyModelToCompetencyModelDtoMapper
    : TwoWayMapperBase<CompetencyModel, CompetencyModelDto>
{
    public override partial CompetencyModelDto Map(CompetencyModel source);
    public override partial void Map(CompetencyModel source, CompetencyModelDto destination);

    public override partial CompetencyModel ReverseMap(CompetencyModelDto destination);
    public override partial void ReverseMap(CompetencyModelDto destination, CompetencyModel source);
}

[Mapper]
public partial class CreateUpdateCompetencyModelDtoToCompetencyModelMapper
    : MapperBase<CreateUpdateCompetencyModelDto, CompetencyModel>
{
    public override partial CompetencyModel Map(CreateUpdateCompetencyModelDto source);
    public override partial void Map(CreateUpdateCompetencyModelDto source, CompetencyModel destination);
}

[Mapper]
public partial class CompetencyModelItemToCompetencyModelItemDtoMapper
    : TwoWayMapperBase<CompetencyModelItem, CompetencyModelItemDto>
{
    public override partial CompetencyModelItemDto Map(CompetencyModelItem source);
    public override partial void Map(CompetencyModelItem source, CompetencyModelItemDto destination);

    public override partial CompetencyModelItem ReverseMap(CompetencyModelItemDto destination);
    public override partial void ReverseMap(CompetencyModelItemDto destination, CompetencyModelItem source);
}

[Mapper]
public partial class AddUpdateCompetencyModelItemDtoToCompetencyModelItemMapper
    : MapperBase<AddUpdateCompetencyModelItemDto, CompetencyModelItem>
{
    public override partial CompetencyModelItem Map(AddUpdateCompetencyModelItemDto source);
    public override partial void Map(AddUpdateCompetencyModelItemDto source, CompetencyModelItem destination);
}
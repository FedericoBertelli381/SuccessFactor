using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Cycles;

[Mapper]
public partial class CycleToCycleDtoMapper : TwoWayMapperBase<Cycle, CycleDto>
{
    public override partial CycleDto Map(Cycle source);
    public override partial void Map(Cycle source, CycleDto destination);

    public override partial Cycle ReverseMap(CycleDto destination);
    public override partial void ReverseMap(CycleDto destination, Cycle source);
}

[Mapper]
public partial class CreateUpdateCycleDtoToCycleMapper : MapperBase<CreateUpdateCycleDto, Cycle>
{
    public override partial Cycle Map(CreateUpdateCycleDto source);
    public override partial void Map(CreateUpdateCycleDto source, Cycle destination);
}
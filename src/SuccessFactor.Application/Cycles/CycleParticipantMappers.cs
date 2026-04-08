using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Cycles;

[Mapper]
public partial class CycleParticipantToDtoMapper : TwoWayMapperBase<CycleParticipant, CycleParticipantDto>
{
    public override partial CycleParticipantDto Map(CycleParticipant source);
    public override partial void Map(CycleParticipant source, CycleParticipantDto destination);
    public override partial CycleParticipant ReverseMap(CycleParticipantDto destination);
    public override partial void ReverseMap(CycleParticipantDto destination, CycleParticipant source);
}
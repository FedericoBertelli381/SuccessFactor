using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Workflow;

[Mapper]
public partial class ProcessPhaseMappers : TwoWayMapperBase<ProcessPhase, ProcessPhaseDto>
{
    public override partial ProcessPhaseDto Map(ProcessPhase source);
    public override partial void Map(ProcessPhase source, ProcessPhaseDto destination);
    public override partial ProcessPhase ReverseMap(ProcessPhaseDto destination);
    public override partial void ReverseMap(ProcessPhaseDto destination, ProcessPhase source);
}

[Mapper]
public partial class CreateUpdateProcessPhaseMapper : MapperBase<CreateUpdateProcessPhaseDto, ProcessPhase>
{
    public override partial ProcessPhase Map(CreateUpdateProcessPhaseDto source);
    public override partial void Map(CreateUpdateProcessPhaseDto source, ProcessPhase destination);
}

[Mapper]
public partial class PhaseTransitionMappers : TwoWayMapperBase<PhaseTransition, PhaseTransitionDto>
{
    public override partial PhaseTransitionDto Map(PhaseTransition source);
    public override partial void Map(PhaseTransition source, PhaseTransitionDto destination);
    public override partial PhaseTransition ReverseMap(PhaseTransitionDto destination);
    public override partial void ReverseMap(PhaseTransitionDto destination, PhaseTransition source);
}

[Mapper]
public partial class CreateUpdatePhaseTransitionMapper : MapperBase<CreateUpdatePhaseTransitionDto, PhaseTransition>
{
    public override partial PhaseTransition Map(CreateUpdatePhaseTransitionDto source);
    public override partial void Map(CreateUpdatePhaseTransitionDto source, PhaseTransition destination);
}
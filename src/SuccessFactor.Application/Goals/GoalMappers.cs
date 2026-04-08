using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using SuccessFactor.Goals;

namespace SuccessFactor.Goals;

[Mapper]
public partial class GoalToGoalDtoMapper : TwoWayMapperBase<Goal, GoalDto>
{
    public override partial GoalDto Map(Goal source);
    public override partial void Map(Goal source, GoalDto destination);

    public override partial Goal ReverseMap(GoalDto destination);
    public override partial void ReverseMap(GoalDto destination, Goal source);
}

[Mapper]
public partial class CreateUpdateGoalDtoToGoalMapper : MapperBase<CreateUpdateGoalDto, Goal>
{
    public override partial Goal Map(CreateUpdateGoalDto source);
    public override partial void Map(CreateUpdateGoalDto source, Goal destination);
}
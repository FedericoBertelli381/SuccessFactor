using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Goals;

[Mapper]
public partial class GoalProgressEntryToGoalProgressEntryDtoMapper
    : TwoWayMapperBase<GoalProgressEntry, GoalProgressEntryDto>
{
    public override partial GoalProgressEntryDto Map(GoalProgressEntry source);
    public override partial void Map(GoalProgressEntry source, GoalProgressEntryDto destination);

    public override partial GoalProgressEntry ReverseMap(GoalProgressEntryDto destination);
    public override partial void ReverseMap(GoalProgressEntryDto destination, GoalProgressEntry source);
}

[Mapper]
public partial class AddGoalProgressDtoToGoalProgressEntryMapper
    : MapperBase<AddGoalProgressDto, GoalProgressEntry>
{
    public override partial GoalProgressEntry Map(AddGoalProgressDto source);
    public override partial void Map(AddGoalProgressDto source, GoalProgressEntry destination);
}
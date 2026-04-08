using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Goals;

[Mapper]
public partial class GoalAssignmentToGoalAssignmentDtoMapper : TwoWayMapperBase<GoalAssignment, GoalAssignmentDto>
{
    public override partial GoalAssignmentDto Map(GoalAssignment source);
    public override partial void Map(GoalAssignment source, GoalAssignmentDto destination);

    public override partial GoalAssignment ReverseMap(GoalAssignmentDto destination);
    public override partial void ReverseMap(GoalAssignmentDto destination, GoalAssignment source);
}

[Mapper]
public partial class CreateUpdateGoalAssignmentDtoToGoalAssignmentMapper : MapperBase<CreateUpdateGoalAssignmentDto, GoalAssignment>
{
    public override partial GoalAssignment Map(CreateUpdateGoalAssignmentDto source);
    public override partial void Map(CreateUpdateGoalAssignmentDto source, GoalAssignment destination);
}
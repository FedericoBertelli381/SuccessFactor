using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Goals;

public class GoalAssignmentDto : EntityDto<Guid>
{
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid GoalId { get; set; }

    public decimal Weight { get; set; }
    public decimal? TargetValue { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    public string Status { get; set; } = default!;
}
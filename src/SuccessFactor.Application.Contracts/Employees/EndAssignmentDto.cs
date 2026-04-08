using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Employees;

public class EndAssignmentDto
{
    [Required] public Guid EmployeeId { get; set; }
    [Required] public Guid ManagerEmployeeId { get; set; }

    public ManagerRelationType RelationType { get; set; } = ManagerRelationType.Line;

    // data fine: se null, usiamo "oggi"
    public DateOnly? EndDate { get; set; }
}
using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Employees;

public class AssignManagerDto
{
    [Required] public Guid EmployeeId { get; set; }
    [Required] public Guid ManagerEmployeeId { get; set; }

    public ManagerRelationType RelationType { get; set; } = ManagerRelationType.Line;
    public bool IsPrimary { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
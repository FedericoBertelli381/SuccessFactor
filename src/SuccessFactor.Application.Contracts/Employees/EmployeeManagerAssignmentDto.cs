using System;

namespace SuccessFactor.Employees;

public class EmployeeManagerAssignmentDto
{
    public Guid EmployeeId { get; set; }
    public Guid ManagerEmployeeId { get; set; }

    public ManagerRelationType RelationType { get; set; }
    public bool IsPrimary { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    // dati “comodi” del manager (join su Employees)
    public string ManagerFullName { get; set; } = default!;
    public string ManagerMatricola { get; set; } = default!;
}
using System;

namespace SuccessFactor.Employees.IdentityLink;

public class LinkedEmployeeDto
{
    public Guid EmployeeId { get; set; }
    public Guid UserId { get; set; }
    public string Matricola { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? EmployeeEmail { get; set; }
    public string UserName { get; set; } = default!;
    public string? UserEmail { get; set; }
}

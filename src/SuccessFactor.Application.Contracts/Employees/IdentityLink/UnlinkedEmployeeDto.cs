using System;

namespace SuccessFactor.Employees.IdentityLink;

public class UnlinkedEmployeeDto
{
    public Guid EmployeeId { get; set; }
    public string Matricola { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
}
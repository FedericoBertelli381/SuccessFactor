using System;

namespace SuccessFactor.Employees.IdentityLink;

public class IdentityUserLookupDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Surname { get; set; }
}
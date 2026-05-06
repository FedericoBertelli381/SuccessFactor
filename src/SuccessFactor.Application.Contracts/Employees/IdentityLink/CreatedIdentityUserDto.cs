using System;
using System.Collections.Generic;

namespace SuccessFactor.Employees.IdentityLink;

public class CreatedIdentityUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Surname { get; set; }
    public List<string> Roles { get; set; } = [];
}

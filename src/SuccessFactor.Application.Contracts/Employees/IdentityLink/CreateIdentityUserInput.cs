using System.Collections.Generic;

namespace SuccessFactor.Employees.IdentityLink;

public class CreateIdentityUserInput
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Surname { get; set; }
    public string Password { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
}

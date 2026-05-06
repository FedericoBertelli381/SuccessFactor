using System;

namespace SuccessFactor.Employees.IdentityLink;

public class ResetIdentityUserPasswordInput
{
    public Guid UserId { get; set; }
    public string NewPassword { get; set; } = default!;
}

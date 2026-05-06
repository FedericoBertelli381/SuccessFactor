using System;

namespace SuccessFactor.Employees.IdentityLink;

public class SetIdentityUserActiveInput
{
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
}

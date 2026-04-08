using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Employees.IdentityLink;

public class LinkEmployeeUserDto
{
    [Required] public Guid EmployeeId { get; set; }
    [Required] public Guid UserId { get; set; }
}
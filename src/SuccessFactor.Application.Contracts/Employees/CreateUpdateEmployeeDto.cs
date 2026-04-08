using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Employees;

public class CreateUpdateEmployeeDto
{
    public Guid? UserId { get; set; }

    [Required]
    [StringLength(50)]
    public string Matricola { get; set; } = default!;

    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = default!;

    [StringLength(256)]
    public string? Email { get; set; }
    public string? OrgUnitId { get; set; }
    public string? JobRoleId { get; set; }

    public bool IsActive { get; set; } = true;
}
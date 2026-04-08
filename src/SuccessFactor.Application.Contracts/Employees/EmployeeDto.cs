using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Employees;

public class EmployeeDto : EntityDto<Guid>
{
    public Guid? UserId { get; set; }
    public string Matricola { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public Guid? OrgUnitId { get; set; }
    public Guid? JobRoleId { get; set; }

    public bool IsActive { get; set; }
}
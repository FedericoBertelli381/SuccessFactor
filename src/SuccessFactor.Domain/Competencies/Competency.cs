using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Competencies;

public class Competency : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    public byte[] RowVer { get; set; } = default!;
}
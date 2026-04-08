using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Competencies.Models;

public class CompetencyModel : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = default!;
    public string ScaleType { get; set; } = "Numeric"; // es. Numeric
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
}
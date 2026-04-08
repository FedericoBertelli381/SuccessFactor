using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Competencies.Models;

public class CompetencyModelItem : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ModelId { get; set; }
    public Guid CompetencyId { get; set; }

    public decimal? Weight { get; set; }
    public bool IsRequired { get; set; }
}
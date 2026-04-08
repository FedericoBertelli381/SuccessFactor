using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Competencies.Models;

public class CompetencyModelItemDto : EntityDto<Guid>
{
    public Guid ModelId { get; set; }
    public Guid CompetencyId { get; set; }
    public decimal? Weight { get; set; }
    public bool IsRequired { get; set; }
}
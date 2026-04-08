using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Competencies.Models;

public class CompetencyModelDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;
    public string ScaleType { get; set; } = default!;
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
}
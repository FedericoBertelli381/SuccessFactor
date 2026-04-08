using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Competencies;

public class CompetencyDto : EntityDto<Guid>
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
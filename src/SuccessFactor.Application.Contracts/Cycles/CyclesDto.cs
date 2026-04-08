using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Cycles;

public class CycleDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;
    public int CycleYear { get; set; }

    public Guid TemplateId { get; set; }
    public Guid? CurrentPhaseId { get; set; }

    public string Status { get; set; } = default!;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
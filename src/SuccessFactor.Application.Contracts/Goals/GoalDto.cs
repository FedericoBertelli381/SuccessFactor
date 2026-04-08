using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Goals;

public class GoalDto : EntityDto<Guid>
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsLibraryItem { get; set; }
    public decimal? DefaultWeight { get; set; }
}
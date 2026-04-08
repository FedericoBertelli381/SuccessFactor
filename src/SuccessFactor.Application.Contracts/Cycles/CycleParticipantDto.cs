using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Cycles;

public class CycleParticipantDto : EntityDto<Guid>
{
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? CurrentPhaseId { get; set; }
    public string Status { get; set; } = default!;
}
using System;

namespace SuccessFactor.Cycles;

public class AdvancePhaseDto
{
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }

    // se null -> “next” automatico
    public Guid? ToPhaseId { get; set; }
}
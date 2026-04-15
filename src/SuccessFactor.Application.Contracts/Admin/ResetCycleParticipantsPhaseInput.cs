using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class ResetCycleParticipantsPhaseInput
{
    [Required]
    public Guid CycleId { get; set; }

    [Required]
    public Guid CurrentPhaseId { get; set; }
}

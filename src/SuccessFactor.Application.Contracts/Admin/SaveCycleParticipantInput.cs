using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveCycleParticipantInput
{
    [Required]
    public Guid CycleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? CurrentPhaseId { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Active";
}

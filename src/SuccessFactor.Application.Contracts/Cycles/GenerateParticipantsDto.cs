using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Cycles;

public class GenerateParticipantsDto
{
    [Required] public Guid CycleId { get; set; }

    // se null -> tutti gli employee attivi del tenant
    public Guid[]? EmployeeIds { get; set; }
}
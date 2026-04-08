using System;

namespace SuccessFactor.My.Dtos;

public class GetMyAssessmentsInput
{
    public Guid? CycleId { get; set; }

    public bool OnlyOpen { get; set; } = true;
}
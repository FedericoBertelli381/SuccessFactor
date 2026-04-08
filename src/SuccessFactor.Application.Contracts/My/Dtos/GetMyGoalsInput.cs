using System;

namespace SuccessFactor.My.Dtos;

public class GetMyGoalsInput
{
    public Guid? CycleId { get; set; }

    public bool OnlyEditable { get; set; }
}
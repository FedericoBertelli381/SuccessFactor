using System;

namespace SuccessFactor.Hr;

public class HrCycleLookupDto
{
    public Guid CycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public string CycleStatus { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

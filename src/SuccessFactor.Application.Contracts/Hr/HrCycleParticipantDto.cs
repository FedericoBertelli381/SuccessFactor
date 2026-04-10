using System;

namespace SuccessFactor.Hr;

public class HrCycleParticipantDto
{
    public Guid EmployeeId { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string ParticipantStatus { get; set; } = string.Empty;
    public Guid? CurrentPhaseId { get; set; }
    public string? CurrentPhaseCode { get; set; }
    public string? CurrentPhaseName { get; set; }
}

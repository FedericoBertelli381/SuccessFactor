using System;

namespace SuccessFactor.Admin;

public class CycleParticipantAdminListItemDto
{
    public Guid ParticipantId { get; set; }
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeEmail { get; set; }
    public Guid? CurrentPhaseId { get; set; }
    public string? CurrentPhaseCode { get; set; }
    public string? CurrentPhaseName { get; set; }
    public string Status { get; set; } = string.Empty;
}

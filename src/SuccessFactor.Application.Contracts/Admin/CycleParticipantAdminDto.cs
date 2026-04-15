using System;
using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class CycleParticipantAdminDto
{
    public Guid? SelectedCycleId { get; set; }
    public Guid? SelectedTemplateId { get; set; }
    public string? SelectedCycleName { get; set; }
    public string? SelectedCycleStatus { get; set; }
    public Guid? SelectedCycleCurrentPhaseId { get; set; }
    public string? SelectedCycleCurrentPhaseCode { get; set; }
    public string? SelectedCycleCurrentPhaseName { get; set; }
    public bool CanEditSelectedCycle { get; set; }

    public List<CycleAdminListItemDto> Cycles { get; set; } = [];
    public List<EmployeeAdminListItemDto> Employees { get; set; } = [];
    public List<WorkflowPhaseLookupDto> Phases { get; set; } = [];
    public List<CycleParticipantAdminListItemDto> Participants { get; set; } = [];
}

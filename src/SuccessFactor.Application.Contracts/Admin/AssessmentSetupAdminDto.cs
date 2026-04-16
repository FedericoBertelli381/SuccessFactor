using System;
using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class AssessmentSetupAdminDto
{
    public Guid? SelectedCycleId { get; set; }
    public string? SelectedCycleName { get; set; }
    public string? SelectedCycleStatus { get; set; }
    public bool CanEditSelectedCycle { get; set; }

    public List<CycleAdminListItemDto> Cycles { get; set; } = [];
    public List<CycleParticipantAdminListItemDto> Participants { get; set; } = [];
    public List<EmployeeAdminListItemDto> Evaluators { get; set; } = [];
    public List<CompetencyModelAdminListItemDto> Models { get; set; } = [];
    public List<AssessmentSetupAdminListItemDto> Assessments { get; set; } = [];
}

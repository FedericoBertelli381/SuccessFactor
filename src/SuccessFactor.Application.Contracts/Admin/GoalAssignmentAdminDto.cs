using System;
using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class GoalAssignmentAdminDto
{
    public Guid? SelectedCycleId { get; set; }
    public string? SelectedCycleName { get; set; }
    public string? SelectedCycleStatus { get; set; }
    public bool CanEditSelectedCycle { get; set; }

    public List<CycleAdminListItemDto> Cycles { get; set; } = [];
    public List<CycleParticipantAdminListItemDto> Participants { get; set; } = [];
    public List<GoalCatalogAdminListItemDto> Goals { get; set; } = [];
    public List<GoalAssignmentAdminListItemDto> Assignments { get; set; } = [];
}

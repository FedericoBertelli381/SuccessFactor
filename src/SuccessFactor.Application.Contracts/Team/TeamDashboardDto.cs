using System;
using System.Collections.Generic;

namespace SuccessFactor.Team;

public class TeamDashboardDto
{
    public Guid ActorEmployeeId { get; set; }
    public string ActorEmployeeName { get; set; } = string.Empty;

    public Guid CycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public string CycleStatus { get; set; } = string.Empty;

    public Guid? SelectedEmployeeId { get; set; }
    public string? SelectedEmployeeName { get; set; }

    public Guid? CurrentPhaseId { get; set; }
    public string? CurrentPhaseCode { get; set; }
    public string RoleCodeUsed { get; set; } = string.Empty;

    public bool CanAdvancePhase { get; set; }

    public List<TeamMemberDto> TeamMembers { get; set; } = [];
}
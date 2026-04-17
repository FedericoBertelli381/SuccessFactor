namespace SuccessFactor.Admin;

public class ImportPerformanceSetupInput
{
    public string? OrgUnitsContent { get; set; }
    public string? JobRolesContent { get; set; }
    public string? ParticipantsContent { get; set; }
    public string? ManagerRelationsContent { get; set; }
    public string? GoalAssignmentsContent { get; set; }
    public string? CompetenciesContent { get; set; }
    public string? CompetencyModelsContent { get; set; }
    public bool UpdateExisting { get; set; }
}

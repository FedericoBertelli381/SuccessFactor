using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class PerformanceSetupImportResultDto
{
    public bool HasErrors { get; set; }
    public int ErrorCount { get; set; }
    public int CreatedOrgUnits { get; set; }
    public int UpdatedOrgUnits { get; set; }
    public int CreatedJobRoles { get; set; }
    public int UpdatedJobRoles { get; set; }
    public int CreatedParticipants { get; set; }
    public int UpdatedParticipants { get; set; }
    public int CreatedManagerRelations { get; set; }
    public int UpdatedManagerRelations { get; set; }
    public int CreatedGoalAssignments { get; set; }
    public int UpdatedGoalAssignments { get; set; }
    public int CreatedCompetencies { get; set; }
    public int UpdatedCompetencies { get; set; }
    public int CreatedCompetencyModels { get; set; }
    public int UpdatedCompetencyModels { get; set; }
    public int CreatedCompetencyModelItems { get; set; }
    public int UpdatedCompetencyModelItems { get; set; }
    public List<PerformanceSetupImportRowResultDto> Rows { get; set; } = [];
}

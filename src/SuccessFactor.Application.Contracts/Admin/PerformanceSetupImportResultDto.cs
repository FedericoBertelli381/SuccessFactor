using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class PerformanceSetupImportResultDto
{
    public bool HasErrors { get; set; }
    public int ErrorCount { get; set; }
    public int CreatedParticipants { get; set; }
    public int UpdatedParticipants { get; set; }
    public int CreatedManagerRelations { get; set; }
    public int UpdatedManagerRelations { get; set; }
    public List<PerformanceSetupImportRowResultDto> Rows { get; set; } = [];
}

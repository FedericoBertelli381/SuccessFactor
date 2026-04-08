using System;

namespace SuccessFactor.Team;

public class UpdateTeamAssessmentItemDto
{
    public Guid CompetencyId { get; set; }
    public int? Score { get; set; }
    public string? Comment { get; set; }
}
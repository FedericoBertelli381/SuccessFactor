using System.Collections.Generic;

namespace SuccessFactor.Team;

public class BulkUpdateTeamAssessmentItemsDto
{
    public List<UpdateTeamAssessmentItemDto> Items { get; set; } = [];
}

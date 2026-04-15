using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class GoalCatalogAdminDto
{
    public List<GoalCatalogAdminListItemDto> Goals { get; set; } = [];
    public List<string> Categories { get; set; } = [];
}

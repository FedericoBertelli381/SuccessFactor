using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class CycleAdminDto
{
    public List<CycleAdminListItemDto> Cycles { get; set; } = [];
    public List<WorkflowTemplateLookupDto> Templates { get; set; } = [];
    public List<WorkflowPhaseLookupDto> Phases { get; set; } = [];
}

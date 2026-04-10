using System;

namespace SuccessFactor.Admin;

public class GetWorkflowAdminInput
{
    public Guid? TemplateId { get; set; }
    public Guid? PhaseId { get; set; }
}

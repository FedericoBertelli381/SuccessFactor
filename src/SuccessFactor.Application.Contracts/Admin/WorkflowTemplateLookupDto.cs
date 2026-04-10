using System;

namespace SuccessFactor.Admin;

public class WorkflowTemplateLookupDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsDefault { get; set; }
}

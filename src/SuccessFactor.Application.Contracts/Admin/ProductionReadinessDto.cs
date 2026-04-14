using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class ProductionReadinessDto
{
    public string TenantId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int LinkedEmployeeCount { get; set; }
    public int ActiveCycleCount { get; set; }
    public int ActiveManagerRelationCount { get; set; }
    public int ActiveParticipantCount { get; set; }
    public int WorkflowTemplateCount { get; set; }
    public int WorkflowPhaseCount { get; set; }
    public int WorkflowFieldPolicyCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<ReadinessCheckDto> Checks { get; set; } = [];
}

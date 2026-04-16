using System;

namespace SuccessFactor.Admin;

public class AssessmentSetupAdminListItemDto
{
    public Guid AssessmentId { get; set; }
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid EvaluatorEmployeeId { get; set; }
    public string EvaluatorMatricola { get; set; } = string.Empty;
    public string EvaluatorName { get; set; } = string.Empty;
    public Guid? ModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public int MissingModelItemCount { get; set; }
    public bool CanSafeRegenerate { get; set; }
}

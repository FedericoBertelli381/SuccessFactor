using System;

namespace SuccessFactor.Hr;

public class HrReportMissingRequiredAssessmentDto
{
    public Guid AssessmentId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EvaluatorName { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int RequiredItemsCount { get; set; }
    public int MissingRequiredCount { get; set; }
}

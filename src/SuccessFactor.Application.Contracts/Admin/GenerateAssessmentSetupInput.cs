using System;

namespace SuccessFactor.Admin;

public class GenerateAssessmentSetupInput
{
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid EvaluatorEmployeeId { get; set; }
    public Guid ModelId { get; set; }
    public string AssessmentType { get; set; } = "Manager";
    public bool SafeRegenerateDraft { get; set; }
}

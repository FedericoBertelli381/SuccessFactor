using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class AssessmentSetupImportResultDto
{
    public bool HasErrors { get; set; }
    public int ErrorCount { get; set; }
    public int CreatedAssessments { get; set; }
    public int RegeneratedAssessments { get; set; }
    public List<AssessmentSetupImportRowResultDto> Rows { get; set; } = [];
}

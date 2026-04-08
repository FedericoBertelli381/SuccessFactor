using System;
using System.Collections.Generic;

namespace SuccessFactor.Competencies.Assessments;

public class CompetencyAssessmentDetailsDto
{
    public Guid AssessmentId { get; set; }

    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid EvaluatorEmployeeId { get; set; }

    public Guid ModelId { get; set; }
    public string AssessmentType { get; set; } = default!;
    public string Status { get; set; } = default!;

    public int MinScore { get; set; }
    public int MaxScore { get; set; }

    public List<CompetencyAssessmentItemDto> Items { get; set; } = new();
}
using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Competencies.Assessments;

public class CreateAssessmentFromModelDto
{
    [Required] public Guid CycleId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    [Required] public Guid EvaluatorEmployeeId { get; set; }

    [Required] public Guid ModelId { get; set; }
    [Required, StringLength(30)] public string AssessmentType { get; set; } = "Manager";
}
using System;
using System.Collections.Generic;
using System.Text;

namespace SuccessFactor.My.Dtos;
public class MyAssessmentItemDto
{
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? RoleCodeUsed { get; set; }

    public Guid? CurrentPhaseId { get; set; }
    public string? CurrentPhaseCode { get; set; }
    public string? CurrentPhaseName { get; set; }
    public Guid AssessmentId { get; set; }

    public string AssessmentType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid TargetEmployeeId { get; set; }

    public string TargetEmployeeName { get; set; } = string.Empty;

    public Guid EvaluatorEmployeeId { get; set; }

    public string EvaluatorEmployeeName { get; set; } = string.Empty;

    public Guid? ModelId { get; set; }

    public string? ModelName { get; set; }

    public int MinScore { get; set; }

    public int MaxScore { get; set; }

    public bool CanEdit { get; set; }

    public bool CanSubmit { get; set; }

    public string ScoreAccess { get; set; } = "Read";

    public string CommentAccess { get; set; } = "Read";

    public string EvidenceAttachmentAccess { get; set; } = "Read";

    public int ItemsCount { get; set; }

    public int RequiredItemsCount { get; set; }

    public int MissingRequiredCount { get; set; }

    public List<MyAssessmentCompetencyItemDto> Items { get; set; } = [];
}

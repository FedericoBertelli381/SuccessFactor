using System;
using System.Collections.Generic;

namespace SuccessFactor.My.Dtos;

public class MyAssessmentsDto
{
    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public Guid CycleId { get; set; }

    public string CycleName { get; set; } = string.Empty;

    public Guid? CurrentPhaseId { get; set; }

    public string? CurrentPhaseCode { get; set; }

    public string RoleCodeUsed { get; set; } = string.Empty;

    public bool CanEdit { get; set; }

    public bool CanSubmitAny { get; set; }

    public List<MyAssessmentItemDto> Items { get; set; } = [];
}



public class MyAssessmentCompetencyItemDto
{
    public Guid CompetencyId { get; set; }

    public string CompetencyCode { get; set; } = string.Empty;

    public string CompetencyName { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public decimal? Weight { get; set; }

    public int? Score { get; set; }

    public string? Comment { get; set; }

    public Guid? EvidenceAttachmentId { get; set; }
}
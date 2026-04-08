using System;
using System.Collections.Generic;

namespace SuccessFactor.My.Dtos;

public class MyDashboardDto
{
    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public Guid CycleId { get; set; }

    public string CycleName { get; set; } = string.Empty;

    public string CycleStatus { get; set; } = string.Empty;

    public Guid? CurrentPhaseId { get; set; }

    public string? CurrentPhaseCode { get; set; }

    public string RoleCodeUsed { get; set; } = string.Empty;

    public bool CanEditGoals { get; set; }

    public bool CanEditAssessments { get; set; }

    public bool CanSubmitAssessments { get; set; }

    public bool CanAdvancePhase { get; set; }

    public int GoalsCount { get; set; }

    public int EditableGoalsCount { get; set; }

    public int OpenAssessmentsCount { get; set; }

    public int MissingRequiredAssessmentsCount { get; set; }

    public List<string> Todo { get; set; } = [];
}
using System;
using System.Collections.Generic;

namespace SuccessFactor.My.Dtos;

public class MyGoalsDto
{
    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public Guid CycleId { get; set; }

    public string CycleName { get; set; } = string.Empty;

    public Guid? CurrentPhaseId { get; set; }

    public string? CurrentPhaseCode { get; set; }

    public string RoleCodeUsed { get; set; } = string.Empty;

    public bool CanEdit { get; set; }

    public List<MyGoalItemDto> Items { get; set; } = [];
}

public class MyGoalItemDto
{
    public Guid AssignmentId { get; set; }

    public Guid GoalId { get; set; }

    public string GoalName { get; set; } = string.Empty;

    public decimal Weight { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal? TargetValue { get; set; }

    public DateOnly? DueDate { get; set; }

    public bool CanEdit { get; set; }

    public string ProgressPercentAccess { get; set; } = "Read";

    public string ActualValueAccess { get; set; } = "Read";

    public string NoteAccess { get; set; } = "Read";

    public string AttachmentAccess { get; set; } = "Read";

    public MyGoalLastProgressDto? LastProgress { get; set; }

    public MyGoalProgressSummaryDto Summary { get; set; } = new();
}

public class MyGoalLastProgressDto
{
    public DateOnly EntryDate { get; set; }

    public decimal? ProgressPercent { get; set; }

    public decimal? ActualValue { get; set; }

    public string? Note { get; set; }

    public Guid? AttachmentId { get; set; }
}

public class MyGoalProgressSummaryDto
{
    public int EntriesCount { get; set; }

    public DateOnly? LastEntryDate { get; set; }

    public decimal? LastProgressPercent { get; set; }

    public decimal? LastActualValue { get; set; }
}
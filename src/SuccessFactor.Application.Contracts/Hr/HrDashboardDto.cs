using System;
using System.Collections.Generic;
using SuccessFactor.My.Dtos;

namespace SuccessFactor.Hr;

public class HrDashboardDto
{
    public Guid? SelectedCycleId { get; set; }
    public string? SelectedCycleName { get; set; }
    public string? SelectedCycleStatus { get; set; }
    public Guid? SelectedEmployeeId { get; set; }
    public string? SelectedEmployeeName { get; set; }
    public string? SelectedEmployeePhaseCode { get; set; }

    public int TotalParticipants { get; set; }
    public int ActiveParticipants { get; set; }
    public int CompletedParticipants { get; set; }

    public List<HrCycleLookupDto> Cycles { get; set; } = [];
    public List<HrCycleParticipantDto> Participants { get; set; } = [];
    public MyGoalsDto? Goals { get; set; }
    public MyAssessmentsDto? Assessments { get; set; }
}

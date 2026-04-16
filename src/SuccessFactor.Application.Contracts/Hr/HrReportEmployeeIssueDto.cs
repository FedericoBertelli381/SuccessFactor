using System;

namespace SuccessFactor.Hr;

public class HrReportEmployeeIssueDto
{
    public Guid EmployeeId { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Issue { get; set; } = string.Empty;
}

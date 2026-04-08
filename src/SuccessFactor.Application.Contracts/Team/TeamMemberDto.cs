using System;

namespace SuccessFactor.Team;

public class TeamMemberDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }

    public bool IsSelected { get; set; }
}
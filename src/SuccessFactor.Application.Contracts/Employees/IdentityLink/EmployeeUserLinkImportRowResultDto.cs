namespace SuccessFactor.Employees.IdentityLink;

public class EmployeeUserLinkImportRowResultDto
{
    public int RowNumber { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? MatchMode { get; set; }
    public string? Message { get; set; }
}

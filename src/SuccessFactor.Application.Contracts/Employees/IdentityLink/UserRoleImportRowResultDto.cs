namespace SuccessFactor.Employees.IdentityLink;

public class UserRoleImportRowResultDto
{
    public int RowNumber { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? Matricola { get; set; }
    public string Roles { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

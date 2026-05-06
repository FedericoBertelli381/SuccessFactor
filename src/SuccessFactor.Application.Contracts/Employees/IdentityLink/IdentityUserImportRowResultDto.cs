namespace SuccessFactor.Employees.IdentityLink;

public class IdentityUserImportRowResultDto
{
    public int RowNumber { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Roles { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

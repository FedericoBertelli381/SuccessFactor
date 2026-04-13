namespace SuccessFactor.Admin;

public class EmployeeImportRowResultDto
{
    public int RowNumber { get; set; }
    public string Matricola { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

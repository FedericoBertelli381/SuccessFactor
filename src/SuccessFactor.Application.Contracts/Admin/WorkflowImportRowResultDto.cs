namespace SuccessFactor.Admin;

public class WorkflowImportRowResultDto
{
    public string Section { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

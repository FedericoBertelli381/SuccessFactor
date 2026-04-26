namespace SuccessFactor.Admin;

public class GoalCatalogImportRowResultDto
{
    public int RowNumber { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

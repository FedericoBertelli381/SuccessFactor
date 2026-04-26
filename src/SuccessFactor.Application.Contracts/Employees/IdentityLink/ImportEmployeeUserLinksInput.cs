namespace SuccessFactor.Employees.IdentityLink;

public class ImportEmployeeUserLinksInput
{
    public string Content { get; set; } = string.Empty;
    public bool UpdateExistingLinks { get; set; }
}

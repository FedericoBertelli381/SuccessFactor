using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class ImportEmployeesInput
{
    [Required]
    public string Content { get; set; } = string.Empty;

    public bool UpdateExisting { get; set; } = true;
}

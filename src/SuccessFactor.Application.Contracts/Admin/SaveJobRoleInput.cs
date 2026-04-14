using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveJobRoleInput
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
}

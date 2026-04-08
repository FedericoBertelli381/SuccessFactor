using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.JobRoles;

public class CreateUpdateJobRoleDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = default!;
}
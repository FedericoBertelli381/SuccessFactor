using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveOrgUnitInput
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentOrgUnitId { get; set; }
}

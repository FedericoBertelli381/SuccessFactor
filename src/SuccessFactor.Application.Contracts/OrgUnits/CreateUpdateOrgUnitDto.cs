using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.OrgUnits;

public class CreateUpdateOrgUnitDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = default!;

    public Guid? ParentOrgUnitId { get; set; }
}
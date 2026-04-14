using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Admin;

public class SaveManagerRelationInput
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ManagerEmployeeId { get; set; }

    [Required]
    [StringLength(30)]
    public string RelationType { get; set; } = "Line";

    public bool IsPrimary { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

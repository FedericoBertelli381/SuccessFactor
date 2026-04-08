using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Goals.Importing;

public class GoalProgressImportItemDto
{
    public int RowNumber { get; set; }

    // Preferibile usare AssignmentId
    public Guid? AssignmentId { get; set; }

    // fallback per risolvere l'assignment se non hai l'id
    public Guid? CycleId { get; set; }
    public string? EmployeeMatricola { get; set; }
    public Guid? GoalId { get; set; }

    [Required] public DateOnly EntryDate { get; set; }
    [Range(0, 100)] public decimal? ProgressPercent { get; set; }
    public decimal? ActualValue { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
    public Guid? AttachmentId { get; set; }
}
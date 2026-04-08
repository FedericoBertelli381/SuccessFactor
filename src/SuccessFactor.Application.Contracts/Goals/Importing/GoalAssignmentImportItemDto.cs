using System;
using System.ComponentModel.DataAnnotations;

namespace SuccessFactor.Goals.Importing;

public class GoalAssignmentImportItemDto
{
    public int RowNumber { get; set; }

    // Identificazione dipendente: preferibile Matricola (da Excel)
    public string? EmployeeMatricola { get; set; }
    public Guid? EmployeeId { get; set; }

    // Goal: o GoalId, oppure titolo (se vuoi crearli al volo)
    public Guid? GoalId { get; set; }
    [StringLength(300)] public string? GoalTitle { get; set; }
    public string? GoalDescription { get; set; }
    [StringLength(100)] public string? GoalCategory { get; set; }
    public bool? IsLibraryItem { get; set; }

    [Range(0, 100)] public decimal Weight { get; set; }
    public decimal? TargetValue { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    [StringLength(30)] public string Status { get; set; } = "Draft";
}
using System;
using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class CompetencyModelAdminDto
{
    public Guid? SelectedModelId { get; set; }
    public string? SelectedModelName { get; set; }
    public bool CanEditSelectedModel { get; set; }
    public int SelectedModelAssessmentCount { get; set; }

    public List<CompetencyModelAdminListItemDto> Models { get; set; } = [];
    public List<CompetencyCatalogAdminListItemDto> Competencies { get; set; } = [];
    public List<CompetencyModelItemAdminListItemDto> Items { get; set; } = [];
}

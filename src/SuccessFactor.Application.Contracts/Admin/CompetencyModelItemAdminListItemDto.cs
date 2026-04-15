using System;

namespace SuccessFactor.Admin;

public class CompetencyModelItemAdminListItemDto
{
    public Guid ModelItemId { get; set; }
    public Guid ModelId { get; set; }
    public Guid CompetencyId { get; set; }
    public string CompetencyCode { get; set; } = string.Empty;
    public string CompetencyName { get; set; } = string.Empty;
    public bool CompetencyIsActive { get; set; }
    public decimal? Weight { get; set; }
    public bool IsRequired { get; set; }
}

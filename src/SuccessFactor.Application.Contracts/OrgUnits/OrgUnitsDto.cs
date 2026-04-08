using System;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.OrgUnits;

public class OrgUnitDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;
    public Guid? ParentOrgUnitId { get; set; }
}
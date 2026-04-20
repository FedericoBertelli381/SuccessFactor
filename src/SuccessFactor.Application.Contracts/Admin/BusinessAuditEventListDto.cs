using System.Collections.Generic;

namespace SuccessFactor.Admin;

public class BusinessAuditEventListDto
{
    public int TotalCount { get; set; }
    public List<BusinessAuditEventDto> Items { get; set; } = [];
}

using System.Collections.Generic;

namespace SuccessFactor.Employees.IdentityLink;

public class UserRoleImportResultDto
{
    public bool HasErrors { get; set; }
    public int ProcessedCount { get; set; }
    public int AddedAssignmentsCount { get; set; }
    public int RemovedAssignmentsCount { get; set; }
    public int ErrorCount { get; set; }
    public List<UserRoleImportRowResultDto> Rows { get; set; } = [];
}

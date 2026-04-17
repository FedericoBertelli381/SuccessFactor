using System;

namespace SuccessFactor.Hr;

public class GetHrExportInput
{
    public HrExportKind ExportKind { get; set; }
    public Guid? CycleId { get; set; }
    public Guid? PhaseId { get; set; }
    public Guid? OrgUnitId { get; set; }
    public Guid? JobRoleId { get; set; }
}

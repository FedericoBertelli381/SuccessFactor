/*
STEP 52 - Performance review indexes

Script idempotente per ambienti SQL Server esistenti.
Allinea gli indici più usati da My, Team, HR dashboard/export e Admin Employees.
*/

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Employees_TenantId_UserId'
      AND object_id = OBJECT_ID(N'dbo.Employees')
)
BEGIN
    CREATE INDEX IX_Employees_TenantId_UserId
        ON dbo.Employees (TenantId, UserId);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Employees_TenantId_OrgUnitId'
      AND object_id = OBJECT_ID(N'dbo.Employees')
)
BEGIN
    CREATE INDEX IX_Employees_TenantId_OrgUnitId
        ON dbo.Employees (TenantId, OrgUnitId);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Employees_TenantId_JobRoleId'
      AND object_id = OBJECT_ID(N'dbo.Employees')
)
BEGIN
    CREATE INDEX IX_Employees_TenantId_JobRoleId
        ON dbo.Employees (TenantId, JobRoleId);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Employees_TenantId_IsActive'
      AND object_id = OBJECT_ID(N'dbo.Employees')
)
BEGIN
    CREATE INDEX IX_Employees_TenantId_IsActive
        ON dbo.Employees (TenantId, IsActive);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Cycles_TenantId_Status_CycleYear'
      AND object_id = OBJECT_ID(N'dbo.Cycles')
)
BEGIN
    CREATE INDEX IX_Cycles_TenantId_Status_CycleYear
        ON dbo.Cycles (TenantId, Status, CycleYear);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CompetencyAssessments_TenantId_CycleId_EmployeeId'
      AND object_id = OBJECT_ID(N'dbo.CompetencyAssessments')
)
BEGIN
    CREATE INDEX IX_CompetencyAssessments_TenantId_CycleId_EmployeeId
        ON dbo.CompetencyAssessments (TenantId, CycleId, EmployeeId);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CompetencyAssessments_TenantId_CycleId_EvaluatorEmployeeId'
      AND object_id = OBJECT_ID(N'dbo.CompetencyAssessments')
)
BEGIN
    CREATE INDEX IX_CompetencyAssessments_TenantId_CycleId_EvaluatorEmployeeId
        ON dbo.CompetencyAssessments (TenantId, CycleId, EvaluatorEmployeeId);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CompetencyAssessments_TenantId_CycleId_Status'
      AND object_id = OBJECT_ID(N'dbo.CompetencyAssessments')
)
BEGIN
    CREATE INDEX IX_CompetencyAssessments_TenantId_CycleId_Status
        ON dbo.CompetencyAssessments (TenantId, CycleId, Status);
END;
GO

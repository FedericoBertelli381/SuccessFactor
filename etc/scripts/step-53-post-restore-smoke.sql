/*
STEP 53 - Post-restore smoke checks

Eseguire sul database ripristinato.
Esempio:
sqlcmd -S "<server-staging>" -d "SuccessFactor_RESTORE_TEST" -E -i ".\etc\scripts\step-53-post-restore-smoke.sql"
*/

SET NOCOUNT ON;

DECLARE @missingObjects table (ObjectName sysname NOT NULL);

INSERT INTO @missingObjects (ObjectName)
SELECT v.ObjectName
FROM (VALUES
    (N'dbo.Employees'),
    (N'dbo.EmployeeManagers'),
    (N'dbo.Cycles'),
    (N'dbo.CycleParticipants'),
    (N'dbo.GoalAssignments'),
    (N'dbo.CompetencyAssessments'),
    (N'dbo.BusinessAuditEvents'),
    (N'dbo.AbpTenants'),
    (N'dbo.AbpUsers')
) AS v(ObjectName)
WHERE OBJECT_ID(v.ObjectName, N'U') IS NULL;

IF EXISTS (SELECT 1 FROM @missingObjects)
BEGIN
    SELECT ObjectName AS MissingObject
    FROM @missingObjects
    ORDER BY ObjectName;

    THROW 51001, 'Post-restore smoke fallito: mancano una o piu tabelle critiche.', 1;
END;

PRINT 'Tabelle critiche presenti.';

SELECT
    (SELECT COUNT(*) FROM dbo.AbpTenants) AS TenantCount,
    (SELECT COUNT(*) FROM dbo.AbpUsers) AS UserCount,
    (SELECT COUNT(*) FROM dbo.Employees) AS EmployeeCount,
    (SELECT COUNT(*) FROM dbo.Cycles) AS CycleCount,
    (SELECT COUNT(*) FROM dbo.CycleParticipants) AS CycleParticipantCount,
    (SELECT COUNT(*) FROM dbo.EmployeeManagers) AS EmployeeManagerCount,
    (SELECT COUNT(*) FROM dbo.GoalAssignments) AS GoalAssignmentCount,
    (SELECT COUNT(*) FROM dbo.CompetencyAssessments) AS CompetencyAssessmentCount,
    (SELECT COUNT(*) FROM dbo.BusinessAuditEvents) AS BusinessAuditEventCount;

SELECT TOP (20)
    t.Id AS TenantId,
    t.Name AS TenantName,
    COUNT(e.Id) AS EmployeeCount,
    SUM(CASE WHEN e.UserId IS NOT NULL THEN 1 ELSE 0 END) AS LinkedEmployeeCount,
    SUM(CASE WHEN e.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveEmployeeCount
FROM dbo.AbpTenants t
LEFT JOIN dbo.Employees e ON e.TenantId = t.Id
GROUP BY t.Id, t.Name
ORDER BY t.Name;

SELECT TOP (20)
    c.TenantId,
    c.Name AS CycleName,
    c.CycleYear,
    c.Status,
    COUNT(cp.Id) AS ParticipantCount
FROM dbo.Cycles c
LEFT JOIN dbo.CycleParticipants cp ON cp.CycleId = c.Id
GROUP BY c.TenantId, c.Name, c.CycleYear, c.Status
ORDER BY c.CycleYear DESC, c.Name;

SELECT TOP (20)
    Status,
    COUNT(*) AS AssessmentCount
FROM dbo.CompetencyAssessments
GROUP BY Status
ORDER BY Status;

SELECT TOP (20)
    Action,
    EntityType,
    COUNT(*) AS EventCount,
    MAX(EventTime) AS LastEventTime
FROM dbo.BusinessAuditEvents
GROUP BY Action, EntityType
ORDER BY LastEventTime DESC;

SELECT
    'Employees without user link' AS CheckName,
    COUNT(*) AS FindingCount
FROM dbo.Employees
WHERE IsActive = 1
  AND UserId IS NULL;

SELECT
    'Active participants without active manager relation' AS CheckName,
    COUNT(*) AS FindingCount
FROM dbo.CycleParticipants cp
INNER JOIN dbo.Employees e ON e.Id = cp.EmployeeId
WHERE cp.Status = N'Active'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.EmployeeManagers em
      WHERE em.EmployeeId = e.Id
        AND em.StartDate <= CAST(GETUTCDATE() AS date)
        AND (em.EndDate IS NULL OR em.EndDate >= CAST(GETUTCDATE() AS date))
  );

PRINT 'Post-restore smoke completato. Valutare i FindingCount e poi avviare smoke test applicativi.';
GO

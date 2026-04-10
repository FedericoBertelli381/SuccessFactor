SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

DECLARE @SourceCycleId UNIQUEIDENTIFIER;
DECLARE @TenantId UNIQUEIDENTIFIER;
DECLARE @TemplateId UNIQUEIDENTIFIER;
DECLARE @CurrentPhaseId UNIQUEIDENTIFIER;
DECLARE @SourceCycleName NVARCHAR(256);

DECLARE @NewCycleId UNIQUEIDENTIFIER;
DECLARE @NewCycleName NVARCHAR(256) = N'Demo Cycle 2027';
DECLARE @NewCycleYear INT = 2027;
DECLARE @NewCycleStatus NVARCHAR(64) = N'Draft';
DECLARE @NewStartDate DATE = '2027-01-01';
DECLARE @NewEndDate DATE = '2027-12-31';

SELECT TOP (1)
    @SourceCycleId = c.CycleId,
    @TenantId = c.TenantId,
    @TemplateId = c.TemplateId,
    @CurrentPhaseId = c.CurrentPhaseId,
    @SourceCycleName = c.Name
FROM dbo.Cycles c
WHERE c.Name = N'Demo Cycle 2026';

IF @SourceCycleId IS NULL
BEGIN
    THROW 50101, 'Ciclo sorgente "Demo Cycle 2026" non trovato.', 1;
END;

SET @NewCycleId = ISNULL(
    (
        SELECT TOP (1) c.CycleId
        FROM dbo.Cycles c
        WHERE c.TenantId = @TenantId
          AND c.Name = @NewCycleName
    ),
    'f4a8b4df-cfd8-472a-b6c8-0b743df78001');

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Cycles c
    WHERE c.CycleId = @NewCycleId
)
BEGIN
    INSERT INTO dbo.Cycles
    (
        [CycleId],
        [TenantId],
        [Name],
        [CycleYear],
        [TemplateId],
        [CurrentPhaseId],
        [Status],
        [StartDate],
        [EndDate],
        [CreatedAt],
        [CreatedByUserId],
        [ModifiedAt],
        [ModifiedByUserId]
    )
    VALUES
    (
        @NewCycleId,
        @TenantId,
        @NewCycleName,
        @NewCycleYear,
        @TemplateId,
        @CurrentPhaseId,
        @NewCycleStatus,
        @NewStartDate,
        @NewEndDate,
        @Now,
        NULL,
        NULL,
        NULL
    );
END;
ELSE
BEGIN
    UPDATE dbo.Cycles
    SET
        [TenantId] = @TenantId,
        [Name] = @NewCycleName,
        [CycleYear] = @NewCycleYear,
        [TemplateId] = @TemplateId,
        [CurrentPhaseId] = @CurrentPhaseId,
        [Status] = @NewCycleStatus,
        [StartDate] = @NewStartDate,
        [EndDate] = @NewEndDate,
        [ModifiedAt] = @Now,
        [ModifiedByUserId] = NULL
    WHERE [CycleId] = @NewCycleId;
END;

INSERT INTO dbo.CycleParticipants
(
    [CycleParticipantId],
    [TenantId],
    [CycleId],
    [EmployeeId],
    [CurrentPhaseId],
    [Status],
    [CreatedAt],
    [CreatedByUserId]
)
SELECT
    NEWID(),
    cp.TenantId,
    @NewCycleId,
    cp.EmployeeId,
    cp.CurrentPhaseId,
    cp.Status,
    @Now,
    cp.CreatedByUserId
FROM dbo.CycleParticipants cp
WHERE cp.CycleId = @SourceCycleId
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.CycleParticipants existing
      WHERE existing.CycleId = @NewCycleId
        AND existing.EmployeeId = cp.EmployeeId
  );

SELECT
    c.CycleId,
    c.Name,
    c.CycleYear,
    c.Status,
    c.StartDate,
    c.EndDate,
    SourceCycleName = @SourceCycleName
FROM dbo.Cycles c
WHERE c.CycleId = @NewCycleId;

SELECT
    COUNT(*) AS ParticipantsCount
FROM dbo.CycleParticipants cp
WHERE cp.CycleId = @NewCycleId;

SELECT
    e.FullName,
    e.Matricola,
    cp.Status AS ParticipantStatus,
    cp.CurrentPhaseId
FROM dbo.CycleParticipants cp
INNER JOIN dbo.Employees e
    ON e.EmployeeId = cp.EmployeeId
WHERE cp.CycleId = @NewCycleId
ORDER BY e.FullName;

COMMIT TRANSACTION;
GO

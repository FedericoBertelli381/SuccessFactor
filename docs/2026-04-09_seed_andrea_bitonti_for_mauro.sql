SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
DECLARE @Today DATE = CAST(@Now AS DATE);

DECLARE @ManagerEmployeeId UNIQUEIDENTIFIER;
DECLARE @ManagerUserId UNIQUEIDENTIFIER;
DECLARE @TenantId UNIQUEIDENTIFIER;
DECLARE @OrgUnitId UNIQUEIDENTIFIER;
DECLARE @JobRoleId UNIQUEIDENTIFIER;

DECLARE @TemplateEmployeeId UNIQUEIDENTIFIER;
DECLARE @TemplateUserId UNIQUEIDENTIFIER;
DECLARE @EmployeeRoleId UNIQUEIDENTIFIER;
DECLARE @CycleId UNIQUEIDENTIFIER;
DECLARE @MgrPhaseId UNIQUEIDENTIFIER;
DECLARE @TemplateAssessmentId UNIQUEIDENTIFIER;
DECLARE @ModelId UNIQUEIDENTIFIER;

DECLARE @NewUserId UNIQUEIDENTIFIER;
DECLARE @NewEmployeeId UNIQUEIDENTIFIER;
DECLARE @NewCycleParticipantId UNIQUEIDENTIFIER;
DECLARE @NewAssessmentId UNIQUEIDENTIFIER;

DECLARE @NewMatricola NVARCHAR(32) = N'M005';
DECLARE @NewUserName NVARCHAR(256) = N'a.bitonti';
DECLARE @NewEmail NVARCHAR(256) = N'a.bitonti@lesoluzioni.net';
DECLARE @NewFullName NVARCHAR(256) = N'Andrea Bitonti';
DECLARE @NewName NVARCHAR(64) = N'Andrea';
DECLARE @NewSurname NVARCHAR(64) = N'Bitonti';
DECLARE @AssessmentType NVARCHAR(64) = N'Manager';

SELECT
    @ManagerEmployeeId = e.EmployeeId,
    @ManagerUserId = e.UserId,
    @TenantId = e.TenantId,
    @OrgUnitId = e.OrgUnitId,
    @JobRoleId = e.JobRoleId
FROM dbo.Employees e
WHERE e.FullName = N'Mauro Lisai';

IF @ManagerEmployeeId IS NULL
BEGIN
    THROW 50001, 'Mauro Lisai non trovato in dbo.Employees.', 1;
END;

SELECT
    @TemplateEmployeeId = e.EmployeeId,
    @TemplateUserId = e.UserId
FROM dbo.Employees e
WHERE e.FullName = N'Cicillo Cacace';

IF @TemplateEmployeeId IS NULL
BEGIN
    THROW 50002, 'Cicillo Cacace non trovato in dbo.Employees.', 1;
END;

SELECT TOP (1)
    @EmployeeRoleId = ur.RoleId
FROM dbo.AbpUserRoles ur
WHERE ur.UserId = @TemplateUserId;

IF @EmployeeRoleId IS NULL
BEGIN
    THROW 50003, 'Ruolo utente template non trovato per Cicillo Cacace.', 1;
END;

SELECT TOP (1)
    @CycleId = cp.CycleId
FROM dbo.CycleParticipants cp
INNER JOIN dbo.ProcessPhases pp ON pp.PhaseId = cp.CurrentPhaseId
WHERE cp.EmployeeId = @TemplateEmployeeId
  AND pp.Code = N'MGR'
ORDER BY cp.CreatedAt DESC;

IF @CycleId IS NULL
BEGIN
    THROW 50004, 'Nessun CycleParticipant in fase MGR trovato per Cicillo Cacace.', 1;
END;

SELECT TOP (1)
    @MgrPhaseId = pp.PhaseId
FROM dbo.ProcessPhases pp
INNER JOIN dbo.Cycles c ON c.TemplateId = pp.TemplateId
WHERE c.CycleId = @CycleId
  AND pp.Code = N'MGR';

IF @MgrPhaseId IS NULL
BEGIN
    THROW 50005, 'PhaseId MGR non trovato per il ciclo selezionato.', 1;
END;

SELECT TOP (1)
    @TemplateAssessmentId = ca.AssessmentId,
    @ModelId = ca.ModelId
FROM dbo.CompetencyAssessments ca
WHERE ca.EmployeeId = @TemplateEmployeeId
  AND ca.CycleId = @CycleId
ORDER BY ca.CreatedAt DESC;

IF @TemplateAssessmentId IS NULL OR @ModelId IS NULL
BEGIN
    THROW 50006, 'Assessment template non trovato o ModelId nullo per Cicillo Cacace.', 1;
END;

SET @NewUserId = ISNULL(
    (SELECT TOP (1) u.Id FROM dbo.AbpUsers u WHERE u.UserName = @NewUserName),
    'b49f8ac4-1cc0-4fb7-9d82-1f8dcfd6d901');

SET @NewEmployeeId = ISNULL(
    (SELECT TOP (1) e.EmployeeId FROM dbo.Employees e WHERE e.Matricola = @NewMatricola),
    '5f2ccf41-98d1-4f8d-a6e1-5282d3d8a401');

SET @NewCycleParticipantId = ISNULL(
    (
        SELECT TOP (1) cp.CycleParticipantId
        FROM dbo.CycleParticipants cp
        WHERE cp.EmployeeId = @NewEmployeeId
          AND cp.CycleId = @CycleId
    ),
    'fbf8f42e-12cf-46b2-bd7d-7adcb19f1c01');

SET @NewAssessmentId = ISNULL(
    (
        SELECT TOP (1) ca.AssessmentId
        FROM dbo.CompetencyAssessments ca
        WHERE ca.EmployeeId = @NewEmployeeId
          AND ca.CycleId = @CycleId
          AND ca.AssessmentType = @AssessmentType
    ),
    '09cb42a4-7874-49ad-9520-3e9f4b4f2501');

IF NOT EXISTS (
    SELECT 1
    FROM dbo.AbpUsers u
    WHERE u.Id = @NewUserId
)
BEGIN
    INSERT INTO dbo.AbpUsers
    (
        [Id],
        [TenantId],
        [UserName],
        [NormalizedUserName],
        [Name],
        [Surname],
        [Email],
        [NormalizedEmail],
        [EmailConfirmed],
        [PasswordHash],
        [SecurityStamp],
        [IsExternal],
        [PhoneNumber],
        [PhoneNumberConfirmed],
        [IsActive],
        [TwoFactorEnabled],
        [LockoutEnd],
        [LockoutEnabled],
        [AccessFailedCount],
        [ShouldChangePasswordOnNextLogin],
        [EntityVersion],
        [LastPasswordChangeTime],
        [LastSignInTime],
        [ExtraProperties],
        [ConcurrencyStamp],
        [CreationTime],
        [CreatorId],
        [LastModificationTime],
        [LastModifierId],
        [IsDeleted],
        [DeleterId],
        [DeletionTime]
    )
    SELECT
        @NewUserId,
        @TenantId,
        @NewUserName,
        UPPER(@NewUserName),
        @NewName,
        @NewSurname,
        @NewEmail,
        UPPER(@NewEmail),
        0,
        u.PasswordHash,
        CONVERT(NVARCHAR(40), REPLACE(NEWID(), '-', '')),
        0,
        NULL,
        0,
        1,
        0,
        NULL,
        0,
        0,
        0,
        1,
        u.LastPasswordChangeTime,
        NULL,
        N'{}',
        CONVERT(NVARCHAR(40), REPLACE(NEWID(), '-', '')),
        @Now,
        @ManagerUserId,
        NULL,
        NULL,
        0,
        NULL,
        NULL
    FROM dbo.AbpUsers u
    WHERE u.Id = @TemplateUserId;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.AbpUserRoles ur
    WHERE ur.UserId = @NewUserId
      AND ur.RoleId = @EmployeeRoleId
)
BEGIN
    INSERT INTO dbo.AbpUserRoles ([UserId], [RoleId], [TenantId])
    VALUES (@NewUserId, @EmployeeRoleId, @TenantId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Employees e
    WHERE e.EmployeeId = @NewEmployeeId
)
BEGIN
    INSERT INTO dbo.Employees
    (
        [EmployeeId],
        [TenantId],
        [UserId],
        [Matricola],
        [FullName],
        [Email],
        [OrgUnitId],
        [JobRoleId],
        [IsActive],
        [CreatedAt],
        [CreatedByUserId],
        [ModifiedAt],
        [ModifiedByUserId]
    )
    VALUES
    (
        @NewEmployeeId,
        @TenantId,
        @NewUserId,
        @NewMatricola,
        @NewFullName,
        @NewEmail,
        @OrgUnitId,
        @JobRoleId,
        1,
        @Now,
        @ManagerUserId,
        NULL,
        NULL
    );
END;
ELSE
BEGIN
    UPDATE dbo.Employees
    SET
        [TenantId] = @TenantId,
        [UserId] = @NewUserId,
        [FullName] = @NewFullName,
        [Email] = @NewEmail,
        [OrgUnitId] = @OrgUnitId,
        [JobRoleId] = @JobRoleId,
        [IsActive] = 1,
        [ModifiedAt] = @Now,
        [ModifiedByUserId] = @ManagerUserId
    WHERE [EmployeeId] = @NewEmployeeId;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.EmployeeManagers em
    WHERE em.EmployeeId = @NewEmployeeId
      AND em.ManagerEmployeeId = @ManagerEmployeeId
      AND em.RelationType = N'Line'
      AND em.EndDate IS NULL
)
BEGIN
    INSERT INTO dbo.EmployeeManagers
    (
        [EmployeeManagerId],
        [TenantId],
        [EmployeeId],
        [ManagerEmployeeId],
        [RelationType],
        [IsPrimary],
        [StartDate],
        [EndDate],
        [CreatedAt],
        [CreatedByUserId],
        [ModifiedAt],
        [ModifiedByUserId]
    )
    VALUES
    (
        'a47cb685-47bc-4cde-b734-3a4d7fe5d001',
        @TenantId,
        @NewEmployeeId,
        @ManagerEmployeeId,
        N'Line',
        1,
        @Today,
        NULL,
        @Now,
        @ManagerUserId,
        NULL,
        NULL
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.CycleParticipants cp
    WHERE cp.EmployeeId = @NewEmployeeId
      AND cp.CycleId = @CycleId
)
BEGIN
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
    VALUES
    (
        @NewCycleParticipantId,
        @TenantId,
        @CycleId,
        @NewEmployeeId,
        @MgrPhaseId,
        N'Active',
        @Now,
        @ManagerUserId
    );
END;
ELSE
BEGIN
    UPDATE dbo.CycleParticipants
    SET
        [CurrentPhaseId] = @MgrPhaseId,
        [Status] = N'Active',
        [CreatedByUserId] = ISNULL([CreatedByUserId], @ManagerUserId)
    WHERE [EmployeeId] = @NewEmployeeId
      AND [CycleId] = @CycleId;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.GoalAssignments ga
    WHERE ga.EmployeeId = @NewEmployeeId
      AND ga.CycleId = @CycleId
)
BEGIN
    INSERT INTO dbo.GoalAssignments
    (
        [AssignmentId],
        [TenantId],
        [CycleId],
        [EmployeeId],
        [GoalId],
        [Weight],
        [TargetValue],
        [StartDate],
        [DueDate],
        [Status],
        [CreatedAt],
        [CreatedByUserId],
        [ModifiedAt],
        [ModifiedByUserId]
    )
    SELECT
        NEWID(),
        ga.TenantId,
        ga.CycleId,
        @NewEmployeeId,
        ga.GoalId,
        ga.Weight,
        ga.TargetValue,
        ga.StartDate,
        ga.DueDate,
        N'InProgress',
        @Now,
        @ManagerUserId,
        NULL,
        NULL
    FROM dbo.GoalAssignments ga
    WHERE ga.EmployeeId = @TemplateEmployeeId
      AND ga.CycleId = @CycleId;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.CompetencyAssessments ca
    WHERE ca.AssessmentId = @NewAssessmentId
)
BEGIN
    INSERT INTO dbo.CompetencyAssessments
    (
        [AssessmentId],
        [TenantId],
        [CycleId],
        [EmployeeId],
        [EvaluatorEmployeeId],
        [AssessmentType],
        [Status],
        [CreatedAt],
        [CreatedByUserId],
        [ModifiedAt],
        [ModifiedByUserId],
        [ModelId]
    )
    VALUES
    (
        @NewAssessmentId,
        @TenantId,
        @CycleId,
        @NewEmployeeId,
        @ManagerEmployeeId,
        @AssessmentType,
        N'Draft',
        @Now,
        @ManagerUserId,
        NULL,
        NULL,
        @ModelId
    );
END;
ELSE
BEGIN
    UPDATE dbo.CompetencyAssessments
    SET
        [EvaluatorEmployeeId] = @ManagerEmployeeId,
        [AssessmentType] = @AssessmentType,
        [Status] = N'Draft',
        [ModelId] = @ModelId,
        [ModifiedAt] = @Now,
        [ModifiedByUserId] = @ManagerUserId
    WHERE [AssessmentId] = @NewAssessmentId;
END;

INSERT INTO dbo.CompetencyAssessmentItems
(
    [ItemId],
    [TenantId],
    [AssessmentId],
    [CompetencyId],
    [Score],
    [Comment],
    [EvidenceAttachmentId]
)
SELECT
    NEWID(),
    @TenantId,
    @NewAssessmentId,
    cmi.CompetencyId,
    NULL,
    NULL,
    NULL
FROM dbo.CompetencyModelItems cmi
WHERE cmi.ModelId = @ModelId
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.CompetencyAssessmentItems cai
      WHERE cai.AssessmentId = @NewAssessmentId
        AND cai.CompetencyId = cmi.CompetencyId
  );

SELECT
    e.EmployeeId,
    e.UserId,
    e.Matricola,
    e.FullName,
    e.Email,
    cp.CycleId,
    pp.Code AS CurrentPhaseCode,
    ca.AssessmentId,
    ca.AssessmentType,
    ca.Status AS AssessmentStatus
FROM dbo.Employees e
INNER JOIN dbo.CycleParticipants cp
    ON cp.EmployeeId = e.EmployeeId
   AND cp.CycleId = @CycleId
INNER JOIN dbo.ProcessPhases pp
    ON pp.PhaseId = cp.CurrentPhaseId
INNER JOIN dbo.CompetencyAssessments ca
    ON ca.EmployeeId = e.EmployeeId
   AND ca.CycleId = cp.CycleId
WHERE e.EmployeeId = @NewEmployeeId;

SELECT
    COUNT(*) AS GoalAssignmentsCount
FROM dbo.GoalAssignments ga
WHERE ga.EmployeeId = @NewEmployeeId
  AND ga.CycleId = @CycleId;

SELECT
    COUNT(*) AS AssessmentItemsCount
FROM dbo.CompetencyAssessmentItems cai
WHERE cai.AssessmentId = @NewAssessmentId;

COMMIT TRANSACTION;
GO

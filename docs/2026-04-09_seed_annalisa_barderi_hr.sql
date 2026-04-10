SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

DECLARE @TenantId UNIQUEIDENTIFIER;
DECLARE @AdminUserId UNIQUEIDENTIFIER;
DECLARE @TemplateEmployeeId UNIQUEIDENTIFIER;
DECLARE @TemplateUserId UNIQUEIDENTIFIER;
DECLARE @OrgUnitId UNIQUEIDENTIFIER;
DECLARE @JobRoleId UNIQUEIDENTIFIER;

DECLARE @HrRoleId UNIQUEIDENTIFIER;
DECLARE @NewUserId UNIQUEIDENTIFIER;
DECLARE @NewEmployeeId UNIQUEIDENTIFIER;

DECLARE @RoleName NVARCHAR(256) = N'HR';
DECLARE @RoleNormalizedName NVARCHAR(256) = N'HR';

DECLARE @NewUserName NVARCHAR(256) = N'a.barderi';
DECLARE @NewEmail NVARCHAR(256) = N'a.barderi@lesoluzioni.net';
DECLARE @NewName NVARCHAR(64) = N'Annalisa';
DECLARE @NewSurname NVARCHAR(64) = N'Barderi';
DECLARE @NewFullName NVARCHAR(256) = N'Annalisa Barderi';
DECLARE @NewMatricola NVARCHAR(32) = N'M007';

SELECT TOP (1)
    @TenantId = t.Id
FROM dbo.AbpTenants t
WHERE t.Name = N'Azienda di test';

IF @TenantId IS NULL
BEGIN
    THROW 50011, 'Tenant "Azienda di test" non trovato.', 1;
END;

SELECT TOP (1)
    @AdminUserId = u.Id
FROM dbo.AbpUsers u
WHERE u.TenantId = @TenantId
  AND u.UserName = N'admin';

IF @AdminUserId IS NULL
BEGIN
    THROW 50012, 'Utente admin tenant non trovato.', 1;
END;

SELECT TOP (1)
    @TemplateEmployeeId = e.EmployeeId,
    @TemplateUserId = e.UserId,
    @OrgUnitId = e.OrgUnitId,
    @JobRoleId = e.JobRoleId
FROM dbo.Employees e
WHERE e.FullName = N'Cicillo Cacace';

IF @TemplateEmployeeId IS NULL OR @TemplateUserId IS NULL
BEGIN
    THROW 50013, 'Employee template "Cicillo Cacace" non trovato o non collegato a un utente.', 1;
END;

SET @HrRoleId = ISNULL(
    (
        SELECT TOP (1) r.Id
        FROM dbo.AbpRoles r
        WHERE r.TenantId = @TenantId
          AND r.NormalizedName = @RoleNormalizedName
    ),
    '4c0bf33e-d554-4d56-98c0-2aa7a00e7001');

SET @NewUserId = ISNULL(
    (
        SELECT TOP (1) u.Id
        FROM dbo.AbpUsers u
        WHERE u.TenantId = @TenantId
          AND u.UserName = @NewUserName
    ),
    '7dfd2a4d-7769-46c0-909c-826ee31d7002');

SET @NewEmployeeId = ISNULL(
    (
        SELECT TOP (1) e.EmployeeId
        FROM dbo.Employees e
        WHERE e.TenantId = @TenantId
          AND e.Matricola = @NewMatricola
    ),
    'f18f2395-2b5d-4fb2-9494-0f7f5d427003');

IF NOT EXISTS (
    SELECT 1
    FROM dbo.AbpRoles r
    WHERE r.Id = @HrRoleId
)
BEGIN
    INSERT INTO dbo.AbpRoles
    (
        [Id],
        [TenantId],
        [Name],
        [NormalizedName],
        [IsDefault],
        [IsStatic],
        [IsPublic],
        [EntityVersion],
        [CreationTime],
        [ExtraProperties],
        [ConcurrencyStamp]
    )
    VALUES
    (
        @HrRoleId,
        @TenantId,
        @RoleName,
        @RoleNormalizedName,
        0,
        0,
        0,
        1,
        @Now,
        N'{}',
        CONVERT(NVARCHAR(40), REPLACE(NEWID(), '-', ''))
    );
END;

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
        @AdminUserId,
        NULL,
        NULL,
        0,
        NULL,
        NULL
    FROM dbo.AbpUsers u
    WHERE u.Id = @TemplateUserId;
END;
ELSE
BEGIN
    UPDATE dbo.AbpUsers
    SET
        [TenantId] = @TenantId,
        [UserName] = @NewUserName,
        [NormalizedUserName] = UPPER(@NewUserName),
        [Name] = @NewName,
        [Surname] = @NewSurname,
        [Email] = @NewEmail,
        [NormalizedEmail] = UPPER(@NewEmail),
        [IsActive] = 1,
        [LastModificationTime] = @Now,
        [LastModifierId] = @AdminUserId
    WHERE [Id] = @NewUserId;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.AbpUserRoles ur
    WHERE ur.UserId = @NewUserId
      AND ur.RoleId = @HrRoleId
      AND ur.TenantId = @TenantId
)
BEGIN
    INSERT INTO dbo.AbpUserRoles ([UserId], [RoleId], [TenantId])
    VALUES (@NewUserId, @HrRoleId, @TenantId);
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
        @AdminUserId,
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
        [Matricola] = @NewMatricola,
        [FullName] = @NewFullName,
        [Email] = @NewEmail,
        [OrgUnitId] = @OrgUnitId,
        [JobRoleId] = @JobRoleId,
        [IsActive] = 1,
        [ModifiedAt] = @Now,
        [ModifiedByUserId] = @AdminUserId
    WHERE [EmployeeId] = @NewEmployeeId;
END;

SELECT
    u.Id AS UserId,
    u.UserName,
    u.Email,
    r.Name AS RoleName,
    e.EmployeeId,
    e.Matricola,
    e.FullName
FROM dbo.AbpUsers u
INNER JOIN dbo.AbpUserRoles ur
    ON ur.UserId = u.Id
INNER JOIN dbo.AbpRoles r
    ON r.Id = ur.RoleId
LEFT JOIN dbo.Employees e
    ON e.UserId = u.Id
WHERE u.Id = @NewUserId;

COMMIT TRANSACTION;
GO

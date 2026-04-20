IF OBJECT_ID(N'[dbo].[BusinessAuditEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BusinessAuditEvents]
    (
        [BusinessAuditEventId] UNIQUEIDENTIFIER NOT NULL,
        [TenantId] UNIQUEIDENTIFIER NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [UserName] NVARCHAR(256) NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [EntityType] NVARCHAR(200) NOT NULL,
        [EntityId] NVARCHAR(100) NULL,
        [EventTime] DATETIME2 NOT NULL,
        [Payload] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_BusinessAuditEvents] PRIMARY KEY ([BusinessAuditEventId])
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_BusinessAuditEvents_TenantId_EventTime'
      AND [object_id] = OBJECT_ID(N'[dbo].[BusinessAuditEvents]')
)
BEGIN
    CREATE INDEX [IX_BusinessAuditEvents_TenantId_EventTime]
        ON [dbo].[BusinessAuditEvents] ([TenantId], [EventTime]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_BusinessAuditEvents_TenantId_Action'
      AND [object_id] = OBJECT_ID(N'[dbo].[BusinessAuditEvents]')
)
BEGIN
    CREATE INDEX [IX_BusinessAuditEvents_TenantId_Action]
        ON [dbo].[BusinessAuditEvents] ([TenantId], [Action]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_BusinessAuditEvents_TenantId_EntityType'
      AND [object_id] = OBJECT_ID(N'[dbo].[BusinessAuditEvents]')
)
BEGIN
    CREATE INDEX [IX_BusinessAuditEvents_TenantId_EntityType]
        ON [dbo].[BusinessAuditEvents] ([TenantId], [EntityType]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_BusinessAuditEvents_TenantId_UserName'
      AND [object_id] = OBJECT_ID(N'[dbo].[BusinessAuditEvents]')
)
BEGIN
    CREATE INDEX [IX_BusinessAuditEvents_TenantId_UserName]
        ON [dbo].[BusinessAuditEvents] ([TenantId], [UserName]);
END;
GO

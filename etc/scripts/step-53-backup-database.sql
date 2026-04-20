/*
STEP 53 - Backup database SuccessFactor

Eseguire in SQLCMD mode da database master.
Esempio:
sqlcmd -S "<server>" -d "master" -E -i ".\etc\scripts\step-53-backup-database.sql" -v DatabaseName="SuccessFactor_PROD" BackupPath="D:\SqlBackups\SuccessFactor_PROD_20260420_220000.bak"
*/

:setvar DatabaseName "SuccessFactor_PROD"
:setvar BackupPath "D:\SqlBackups\SuccessFactor_PROD_manual.bak"

SET NOCOUNT ON;

DECLARE @databaseName sysname = N'$(DatabaseName)';
DECLARE @backupPath nvarchar(4000) = N'$(BackupPath)';
DECLARE @sql nvarchar(max);

IF DB_ID(@databaseName) IS NULL
BEGIN
    THROW 51000, 'DatabaseName non trovato. Verificare il parametro SQLCMD DatabaseName.', 1;
END;

PRINT CONCAT('Backup database: ', @databaseName);
PRINT CONCAT('Backup path: ', @backupPath);

SET @sql = N'
BACKUP DATABASE ' + QUOTENAME(@databaseName) + N'
TO DISK = @backupPath
WITH COPY_ONLY, INIT, COMPRESSION, CHECKSUM, STATS = 10;';

EXEC sys.sp_executesql
    @sql,
    N'@backupPath nvarchar(4000)',
    @backupPath = @backupPath;

RESTORE VERIFYONLY
FROM DISK = @backupPath
WITH CHECKSUM;

PRINT 'Backup completato e verificato con RESTORE VERIFYONLY.';
GO

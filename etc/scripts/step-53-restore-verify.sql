/*
STEP 53 - Verify backup e restore test guidato

Eseguire in SQLCMD mode da database master.
Lo script esegue RESTORE VERIFYONLY e mostra i logical file name da usare per un restore test.

Esempio:
sqlcmd -S "<server-staging>" -d "master" -E -i ".\etc\scripts\step-53-restore-verify.sql" -v BackupPath="D:\SqlBackups\SuccessFactor_PROD_20260420_220000.bak"
*/

:setvar BackupPath "D:\SqlBackups\SuccessFactor_PROD_manual.bak"

SET NOCOUNT ON;

DECLARE @backupPath nvarchar(4000) = N'$(BackupPath)';

PRINT CONCAT('Verifica backup: ', @backupPath);

RESTORE VERIFYONLY
FROM DISK = @backupPath
WITH CHECKSUM;

PRINT 'Logical file name nel backup:';

RESTORE FILELISTONLY
FROM DISK = @backupPath;

PRINT 'Esempio restore test, da adattare con LogicalName e percorsi reali:';
PRINT 'RESTORE DATABASE [SuccessFactor_RESTORE_TEST]';
PRINT 'FROM DISK = N''<BackupPath>''';
PRINT 'WITH MOVE N''<LogicalDataName>'' TO N''D:\SqlData\SuccessFactor_RESTORE_TEST.mdf'',';
PRINT '     MOVE N''<LogicalLogName>'' TO N''D:\SqlLogs\SuccessFactor_RESTORE_TEST_log.ldf'',';
PRINT '     RECOVERY, REPLACE, CHECKSUM, STATS = 10;';
GO

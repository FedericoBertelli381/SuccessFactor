# STEP 53 - Backup, restore e disaster recovery

Data review: 2026-04-20

## Obiettivo

Preparare una procedura operativa minima per proteggere l'ambiente SuccessFactor prima e dopo deploy, con:

- backup database.
- verifica backup.
- restore test.
- retention.
- rollback deploy.
- controlli post-restore.

Questa procedura usa placeholder e non deve contenere connection string o credenziali.

## Ambiti coperti

Database:

- SQL Server database applicativo SuccessFactor.
- Tabelle ABP Identity/Tenant incluse nello stesso database.
- Tabelle business `Employees`, `Cycles`, `CycleParticipants`, `GoalAssignments`, `CompetencyAssessments`, `BusinessAuditEvents`.

Applicazione:

- artefatto pubblicato della web app Blazor.
- configurazioni ambiente.
- script SQL applicati manualmente.

Fuori ambito per ora:

- backup file allegati, se in futuro verranno introdotti upload/documenti.
- backup provider esterni SSO/SMTP.
- replica geografica o alta disponibilita.

## RPO e RTO iniziali

Target consigliato per prima produzione:

- RPO: massimo 24 ore di perdita dati in caso di disastro completo.
- RTO: ripristino entro 4 ore lavorative.

Se il processo performance viene usato durante finestre critiche di valutazione, portare temporaneamente il backup a frequenza oraria o pre/post operazioni massive.

## Backup database

Quando eseguirlo:

- ogni giorno fuori orario lavorativo.
- prima di ogni deploy.
- prima di script SQL manuali.
- prima di import massivi.
- prima di modifiche di configurazione workflow rilevanti.

Script:

- `etc/scripts/step-53-backup-database.sql`

Esecuzione esempio:

```powershell
sqlcmd -S "<server>" -d "master" -E -i ".\etc\scripts\step-53-backup-database.sql" -v DatabaseName="SuccessFactor_PROD" BackupPath="D:\SqlBackups\SuccessFactor_PROD_20260420_220000.bak"
```

Note operative:

- eseguire il backup da `master`.
- usare `COPY_ONLY` per i backup manuali pre-deploy, cosi non si altera la catena dei backup pianificati.
- verificare che il percorso backup sia locale o raggiungibile dal servizio SQL Server, non dalla macchina client.
- salvare il backup fuori dalla cartella applicativa.

## Restore test

Frequenza minima:

- una volta prima del go-live.
- una volta dopo ogni cambio importante di schema.
- almeno mensile in produzione.

Procedura:

1. Copiare il `.bak` su ambiente staging/test.
2. Eseguire `RESTORE VERIFYONLY`.
3. Ripristinare su un database temporaneo, non sul database live.
4. Eseguire lo script post-restore.
5. Avviare l'app puntando al database ripristinato.
6. Eseguire smoke test Self, Team, HR, Admin.
7. Eliminare o archiviare il database temporaneo secondo policy interna.

Script:

- `etc/scripts/step-53-restore-verify.sql`
- `etc/scripts/step-53-post-restore-smoke.sql`

## Retention

Policy minima consigliata:

- backup giornalieri: 14 giorni.
- backup settimanali: 8 settimane.
- backup mensili: 12 mesi.
- backup pre-deploy: conservarli almeno fino al deploy successivo completato e validato.

Controlli:

- spazio disco residuo sul volume backup.
- presenza backup ultimo giorno.
- presenza almeno un backup verificato negli ultimi 30 giorni.
- accesso limitato ai soli operatori autorizzati.

## Rollback deploy

Prima del deploy:

- annotare commit/tag Git rilasciato.
- salvare artefatto applicativo corrente.
- esportare configurazione ambiente corrente.
- eseguire backup database pre-deploy.
- verificare esito `RESTORE VERIFYONLY` del backup pre-deploy.

Durante il deploy:

- applicare migrazioni/script solo dopo backup riuscito.
- registrare ogni script applicato, ordine, ora e operatore.
- non cancellare l'artefatto precedente.

Rollback applicativo senza rollback DB:

- usare quando il deploy applicativo fallisce ma non sono state applicate modifiche DB incompatibili.
- ripubblicare artefatto precedente.
- ripristinare configurazione precedente.
- riavviare app pool/servizio.
- eseguire smoke test.

Rollback applicativo con rollback DB:

- usare solo quando le modifiche DB rendono incompatibile la versione precedente o i dati sono stati corrotti.
- fermare applicazione.
- ripristinare il backup pre-deploy su database target secondo procedura DBA.
- ripubblicare artefatto precedente.
- rieseguire post-restore smoke SQL e smoke test applicativi.

Regola di prudenza:

- se dopo il deploy sono stati inseriti dati utente validi, valutare con il referente funzionale prima di ripristinare il DB pre-deploy perche il restore cancellera quei dati.

## Verifica post-restore

Lo script `step-53-post-restore-smoke.sql` controlla:

- esistenza tabelle business principali.
- tenant presenti.
- employee presenti.
- link user-employee.
- cicli e participant.
- relazioni manager.
- assessment e goal.
- audit table e ultimi eventi.

Lo script produce risultati leggibili e solleva errore solo se mancano oggetti critici.

## Smoke test applicativi dopo restore

Test minimi:

- login admin.
- `/admin/readiness?__tenant=<tenant>` senza status `Error` inattesi.
- `/my?__tenant=<tenant>` con employee collegato.
- `/team?__tenant=<tenant>` con manager che ha subordinati attivi.
- `/hr?__tenant=<tenant>` con utente HR.
- `/admin/audit?__tenant=<tenant>` filtrando ultimi eventi.
- export HR CSV su ciclo di test.

Test negativi:

- utente non admin non vede menu Admin.
- utente non linkato vede errore business leggibile su `/my`, senza bloccare l'app.
- manager non puo aprire target fuori scope.

## Checklist finale STEP 53

- Runbook disponibile in repository.
- Script backup disponibile.
- Script verify restore disponibile.
- Script smoke post-restore disponibile.
- Retention documentata.
- Piano rollback deploy documentato.
- Test restore completo da eseguire su staging prima di produzione.

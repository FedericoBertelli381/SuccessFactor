# STEP 54 - Deploy staging

Data review: 2026-04-20

## Obiettivo

Preparare ed eseguire un deploy completo in ambiente staging prima della produzione.

Lo staging deve validare:

- configurazione ambiente.
- connection string.
- tenant.
- migrazione database.
- SSO se disponibile.
- SMTP solo se disponibile.
- smoke test applicativo completo.
- piano correzioni post-test.

## Vincolo aperto dallo STEP 53

Sul server di test l'utente applicativo non ha permessi di backup database e al momento il DBA non e disponibile.

Decisione operativa:

- non bloccare lo STEP 54.
- non eseguire script distruttivi o restore DB.
- prima di qualunque deploy su produzione, riprendere lo STEP 53 con DBA disponibile.
- su staging procedere con migrazione controllata solo se il database e gia sacrificabile o se esiste una copia recuperabile.

## Artefatti introdotti

- `src/SuccessFactor.Blazor/appsettings.Staging.example.json`
- `src/SuccessFactor.DbMigrator/appsettings.Staging.example.json`
- `etc/scripts/step-54-publish-staging.ps1`

I file `.example.json` non contengono segreti. Copiarli come `appsettings.Staging.json` solo sull'ambiente target o in una cartella non versionata.

## Prerequisiti staging

Server:

- Windows Server con hosting bundle ASP.NET Core/.NET 10 installato.
- IIS o servizio equivalente configurato per la web app.
- certificato HTTPS valido per l'URL staging.
- accesso alla cartella applicativa.
- accesso SQL Server con permessi necessari a leggere/scrivere il database applicativo.

Database:

- database staging dedicato, non produzione.
- login SQL o Integrated Security configurati.
- permessi per eseguire migrazioni tramite DbMigrator.
- se possibile, backup o snapshot disponibile prima di migrare.

Configurazione:

- `ASPNETCORE_ENVIRONMENT=Staging`.
- `App:SelfUrl` uguale all'URL HTTPS staging.
- `AuthServer:Authority` uguale all'URL HTTPS staging.
- `RemoteServices:Default:BaseUrl` uguale all'URL HTTPS staging.
- `ConnectionStrings:Default` valorizzata fuori repository.

## Preparazione configurazione

1. Copiare `src/SuccessFactor.Blazor/appsettings.Staging.example.json` in `appsettings.Staging.json`.
2. Sostituire i placeholder:
   - `<staging-url>`
   - `<sql-server>`
   - `<database>`
   - `<user>`
   - `<password>`
   - `<certificate-passphrase>`
   - `<string-encryption-passphrase>`
3. Copiare `src/SuccessFactor.DbMigrator/appsettings.Staging.example.json` in `appsettings.Staging.json`.
4. Allineare la connection string del DbMigrator a quella della web app.
5. Se SSO non e disponibile, lasciare `Sso:OpenIdConnect:Enabled=false`.
6. Se SMTP non e testabile, lasciare email disabilitata o configurata con provider non produttivo.

## Build e publish

Da root repository:

```powershell
.\etc\scripts\step-54-publish-staging.ps1
```

Output predefinito:

```text
artifacts\staging\SuccessFactor.Blazor
```

Il comando pubblica la web app in Release senza restore. Se serve un restore, eseguirlo prima in ambiente di sviluppo/CI.

## Migrazione database staging

Con `appsettings.Staging.json` pronto nel progetto DbMigrator:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Staging"
dotnet run --project .\src\SuccessFactor.DbMigrator\SuccessFactor.DbMigrator.csproj --no-restore
```

Prima della migrazione:

- confermare di puntare al database staging.
- verificare che non sia il database produzione.
- se possibile, ottenere conferma che esiste un backup/snapshot.

Dopo la migrazione:

- eseguire `etc/scripts/step-53-post-restore-smoke.sql` adattandolo come smoke DB anche su staging.
- verificare presenza tenant, employee, cycle, participant e audit table.

## Deploy applicativo

1. Fermare app pool o servizio staging.
2. Copiare il contenuto di `artifacts\staging\SuccessFactor.Blazor` nella cartella applicativa staging.
3. Copiare `appsettings.Staging.json` target nella cartella applicativa.
4. Impostare `ASPNETCORE_ENVIRONMENT=Staging`.
5. Avviare app pool o servizio.
6. Aprire `https://<staging-url>/health-status`.
7. Aprire la home e verificare login.

## Smoke test completo staging

Eseguire con tenant esplicito quando richiesto:

- `/admin/readiness?__tenant=<tenant>`: nessun `Error` inatteso.
- `/my?__tenant=<tenant>` con employee collegato.
- `/team?__tenant=<tenant>` con manager e subordinati attivi.
- `/hr?__tenant=<tenant>` con utente HR.
- `/hr/reports?__tenant=<tenant>`.
- `/hr/performance-dashboard?__tenant=<tenant>`.
- `/admin/audit?__tenant=<tenant>` con filtro data/utente/azione.
- `/admin/notifications?__tenant=<tenant>` solo se SMTP/provider e configurazione sono disponibili.

Smoke admin:

- admin menu visibile solo ad admin.
- utente HR non vede voci Admin non autorizzate.
- utente non linkato su `/my` vede errore business leggibile e l'app non si blocca.
- manager non puo aprire target fuori scope.

Smoke export/import:

- export CSV HR su ciclo di test.
- import CSV con errore e verifica no-partial-save.
- import CSV valido solo su dati di staging sacrificabili.

## SSO e SMTP

SSO:

- se il provider aziendale non e disponibile, lasciare `Sso:OpenIdConnect:Enabled=false`.
- il test SSO end-to-end resta da ricordare nello STEP 55.

SMTP:

- se il relay non consente invio completo o manca autorizzazione, non bloccare staging.
- il test SMTP end-to-end resta da ricordare nello STEP 55.

## Criteri di uscita STEP 54

STEP 54 puo considerarsi chiuso quando:

- artefatto Release pubblicabile creato.
- template staging disponibili senza segreti.
- procedura migrazione documentata.
- smoke test staging documentati.
- vincolo backup DBA tracciato come rischio.

Il deploy reale su server puo essere eseguito quando sono disponibili URL, credenziali e finestra operativa.

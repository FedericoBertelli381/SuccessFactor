# STEP 51 - Security review

Data review: 2026-04-20

## Esito sintetico

Review pre-deploy completata sulle superfici applicative principali:

- tenant isolation.
- enforcement ruoli server-side.
- manipolazione `targetEmployeeId`.
- scope manager via `EmployeeManagers`.
- endpoint admin e workflow.
- import sanitization.
- export permissions.

## Fix applicati

### Endpoint CRUD di configurazione admin-only

Sono stati protetti con `[Authorize(Roles = SuccessFactorRoles.Admin)]` i CRUD legacy/laterali che non passavano dalle pagine Admin hardenizzate:

- `JobRoleAppService`.
- `OrgUnitAppService`.
- `ProcessTemplateAppService`.
- `ProcessPhaseAppService`.
- `PhaseTransitionAppService`.
- `PhaseRolePermissionAppService`.
- `PhaseFieldPolicyAppService`.

Motivo: questi servizi espongono configurazione organizzativa e workflow. Anche se la UI usa servizi Admin dedicati, gli endpoint convenzionali non devono restare accessibili a utenti non admin.

### Target employee fuori scope

`WorkflowAccessAppService` e `WorkflowAuthorizationService` ora negano esplicitamente il target fuori perimetro con `TargetEmployeeNotInManagerScope`.

Regola attesa:

- admin/HR: accesso come ruolo HR.
- self: accesso come Employee.
- manager attivo in `EmployeeManagers`: accesso come Manager.
- altro target: negato.

Prima, il caso non autorizzato ricadeva sul ruolo `Employee`, con rischio di permessi errati se il workflow aveva fallback permissivi.

### Messaggi UI

Il formatter errori Blazor traduce `TargetEmployeeNotInManagerScope` in un messaggio utente leggibile:

`Dipendente non disponibile nel perimetro dell'utente corrente.`

## Verifiche effettuate

### Tenant isolation

Le entity multi-tenant sono lette tramite repository ABP, quindi restano soggette ai filtri tenant correnti. I servizi admin verificano anche `CurrentTenant.Id` prima delle operazioni di setup/import.

Rischio residuo: verificare in staging che ogni chiamata manuale abbia tenant valorizzato quando si usa query string `__tenant`.

### Role enforcement server-side

Le superfici operative sono protette server-side:

- My: `[Authorize]` e contesto derivato da `CurrentUser`.
- Team: `[Authorize]` e scope manager via resolver.
- HR/report/export: `[Authorize]` più `IsAdminOrHr`.
- Admin: `[Authorize]` più `IsAdmin`.
- CRUD workflow/config legacy: ora admin-only.

### Query target employee

Le query Team validano `targetEmployeeId` contro `EmployeeManagers`. La review ha corretto anche i servizi workflow condivisi per impedire fallback non autorizzati.

### Manager scope

Lo scope manager usa relazioni attive:

- `ManagerEmployeeId`.
- `EmployeeId`.
- `StartDate <= today` se valorizzata.
- `EndDate >= today` se valorizzata.
- employee target attivo.

### Import sanitization

Import employee e import setup performance hanno validazione no-partial-save, normalizzazione campi, controllo duplicati e audit evento completamento. I servizi sono admin-only.

Rischio residuo: aggiungere in uno step futuro limiti espliciti di dimensione contenuto import e numero righe per evitare payload eccessivi.

### Export permissions

Export HR passa da `HrReportsAppService.ExportCsvAsync`, protetto da `IsAdminOrHr`, e traccia audit `HrReportExported`.

Rischio residuo: valutare rate limit o audit alert su export frequenti in produzione.

## Test manuali consigliati

1. Utente `Dipendente`: provare chiamata/pagina Admin. Atteso: menu assente e endpoint admin negato.
2. Utente `Responsabile`: provare a forzare un `targetEmployeeId` non subordinato. Atteso: banner/errore `Dipendente non disponibile nel perimetro dell'utente corrente.`
3. Utente `Responsabile`: aprire target subordinato attivo. Atteso: accesso consentito.
4. Utente `HR`: aprire HR report/export. Atteso: accesso consentito.
5. Utente `Dipendente`: provare export HR o endpoint admin. Atteso: negato.
6. Utente non autenticato: chiamare endpoint My/Team/HR/Admin. Atteso: redirect/login o 401.
7. Import setup performance con utente non admin. Atteso: negato.
8. Import setup performance con admin e file valido. Atteso: validazione no-partial-save e audit.


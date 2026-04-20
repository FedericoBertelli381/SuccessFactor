# STEP 52 - Performance review

Data review: 2026-04-20

## Esito sintetico

Review completata sulle viste principali:

- My dashboard.
- Team dashboard/report.
- HR dashboard/report/export.
- Admin Employees.

## Fix applicati

### My assessments

`MyAssessmentsAppService` non carica più tutti gli `Employees` del tenant.

Ora carica solo gli employee realmente referenziati dagli assessment restituiti:

- `EmployeeId`.
- `EvaluatorEmployeeId`.

Impatto atteso:

- minore memoria allocata.
- meno righe lette su tenant con molti dipendenti.
- beneficio immediato su pagina `/my` e dettaglio assessment.

### Indici database consigliati

Aggiornato il mapping EF e aggiunto script SQL idempotente:

`etc/scripts/step-52-performance-indexes.sql`

Indici aggiunti o consigliati:

- `Employees(TenantId, UserId)` per lookup utente -> Employee.
- `Employees(TenantId, OrgUnitId)` per filtri/report per OrgUnit.
- `Employees(TenantId, JobRoleId)` per filtri/report per JobRole.
- `Employees(TenantId, IsActive)` per liste operative e import/check.
- `Cycles(TenantId, Status, CycleYear)` per selezione ciclo attivo/recente.
- `CompetencyAssessments(TenantId, CycleId, EmployeeId)` per My/Team/HR target.
- `CompetencyAssessments(TenantId, CycleId, EvaluatorEmployeeId)` per viste evaluator/manager.
- `CompetencyAssessments(TenantId, CycleId, Status)` per report stato assessment.

## Query già coperte

Sono già presenti indici utili su:

- `GoalAssignments(TenantId, CycleId, EmployeeId)`.
- `GoalProgressEntries(AssignmentId, EntryDate)`.
- `CycleParticipants(CycleId, EmployeeId)` unique.
- `CycleParticipants(TenantId, CycleId)`.
- `EmployeeManagers(TenantId, ManagerEmployeeId)`.
- `EmployeeManagers(TenantId, EmployeeId)`.
- `BusinessAuditEvents(TenantId, EventTime/Action/EntityType/UserName)`.

## Rischi residui

### Admin Employees

La pagina carica ancora tutti gli employee in una sola risposta. Va bene per dataset piccoli/medi, ma su tenant grandi conviene introdurre:

- ricerca testo.
- filtro active/orgunit/jobrole.
- paginazione server-side.

### Report HR export

Gli export CSV caricano i dati in memoria. Per volumi molto grandi valutare:

- limite massimo righe esportabili.
- job asincrono.
- streaming file.
- audit alert su export massivi.

### Dashboard aggregate

Le dashboard calcolano aggregazioni lato application dopo fetch mirati. Per volumi molto grandi valutare:

- query aggregate SQL lato database.
- viste indicizzate/reporting table.
- cache breve per dashboard HR aggregate.

## Test performance consigliati

Dataset minimo di prova:

- 500 employee.
- 10 org unit.
- 20 job role.
- 1 ciclo attivo.
- 500 participants.
- 2 goal per participant.
- 1 self assessment e 1 manager assessment per participant.
- almeno 3 progress entry per goal.

Misurare:

- `/my`: caricamento iniziale sotto 2 secondi.
- `/team`: caricamento manager con 20 subordinati sotto 2 secondi.
- HR dashboard target: cambio employee sotto 2 secondi.
- HR report: caricamento sotto 4 secondi.
- HR export CSV participants/goals/assessments sotto 10 secondi su dataset minimo.
- Admin Employees: caricamento sotto 3 secondi su 500 employee.

## Prossimi miglioramenti performance

- Introdurre paginazione server-side su Admin Employees se il tenant supera 1.000 employee.
- Spostare export grandi su job asincrono se superano 10.000 righe.
- Usare aggregazioni SQL per dashboard HR se i tempi superano i target.


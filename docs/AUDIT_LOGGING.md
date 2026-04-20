# Audit log business

Lo step 48 aggiunge un tracciamento applicativo esplicito per le azioni business piu sensibili e una pagina Admin per consultarlo.

Gli eventi sono salvati nella tabella `dbo.BusinessAuditEvents` e sono anche scritti nei log applicativi come record strutturati con prefisso `BusinessAudit`. Il middleware ABP `UseAuditing()` resta attivo per l'audit tecnico delle richieste, mentre questi eventi aggiungono contesto funzionale leggibile da UI, monitoraggio, SIEM o log collector.

## Pagina Admin

La pagina e disponibile dal menu Admin:

```text
Admin Audit Log
```

Route:

```text
/admin/audit-log
```

Se il database non viene aggiornato tramite migration EF, eseguire prima lo script:

```text
etc/scripts/step-48-create-business-audit-events.sql
```

Filtri disponibili:

- data da/a;
- utente, per username o id;
- azione;
- entita;
- entity id;
- tenant id;
- numero righe per pagina.

## Eventi tracciati

| Evento | Area | Dati principali |
| --- | --- | --- |
| `AssessmentItemUpdated` | Valutazioni | assessment, ciclo, employee, evaluator, competency, presenza score/comment/evidenza |
| `AssessmentSubmitted` | Valutazioni | assessment, ciclo, employee, evaluator, tipo assessment |
| `CyclePhaseStarted` | Workflow ciclo | participant, ciclo, employee, fase iniziale |
| `CyclePhaseAdvanced` | Workflow ciclo | participant, ciclo, employee, fase origine, fase destinazione, completamento |
| `EmployeeImportCompleted` | Import admin | righe, creati, aggiornati, update existing |
| `PerformanceSetupImportCompleted` | Import performance | contatori create/update per sezione, righe, update existing |
| `WorkflowRolePermissionSaved` | Admin workflow | template, fase, ruolo, permessi, tipo modifica |
| `WorkflowRolePermissionDeleted` | Admin workflow | template, fase, ruolo |
| `WorkflowFieldPolicySaved` | Admin workflow | template, fase, field key, ruolo, accesso, required, tipo modifica |
| `WorkflowFieldPolicyDeleted` | Admin workflow | template, fase, field key, ruolo |
| `EmployeeUserLinked` | Identity linking | employee e utente collegato |
| `EmployeeUserUnlinked` | Identity linking | employee e utente precedente |
| `HrReportExported` | Export HR | tipo export, filtri, ciclo, conteggi, nome file |

## Dati esclusi

Per ridurre il rischio di esposizione nei log non vengono salvati:

- contenuto CSV importato o esportato;
- commenti testuali delle valutazioni;
- valori puntuali degli score;
- email o nominativi se non gia inclusi dal contesto standard del logger.

## Verifica operativa

Eseguire una delle azioni tracciate e cercare nei log applicativi la stringa `BusinessAudit` insieme al nome evento atteso.
In alternativa aprire `Admin Audit Log` e filtrare per azione o utente.

Esempio:

```text
BusinessAudit AssessmentSubmitted CompetencyAssessment ...
```

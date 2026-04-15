# NEXT STEPS - SuccessFactor

## Stato attuale
- Step 13 completato
- Manager assessment item edit funzionante
- Self e Team flow stabili

## Prossimo step

### STEP 14 — Bulk save assessment items (manager)
Obiettivo:
- permettere modifica multipla item
- salvare tutto in una sola operazione
- evitare save riga per riga

Include:
## 14.2 dirty tracking lato UI
- bottone "Save all"
- validazione required
- messaggio di successo

## Step successivi

### STEP 15
- migliorare UX assessment (loading, feedback, errori)

### STEP 16
- HR flow base (visione HR sui cicli)

### STEP 17+
- admin config
- import dati
- hardening produzione

---

## Stato aggiornato dopo STEP 24

- STEP 14 completato: bulk save assessment items manager.
- STEP 15 completato: polish UX assessment manager.
- STEP 16 completato: HR flow base.
- STEP 17 completato: HR target flow read-only con cambio ciclo e selezione dipendente.
- STEP 18 completato: HR detail dedicati per goals e assessments target.
- STEP 19 completato: admin config minima workflow e permessi.
- STEP 20 completato: identity linking admin tra utenti ABP e dipendenti.
- STEP 21 completato: admin employee management base.
- STEP 22 completato: import employee CSV base.
- STEP 23 completato: hardening admin tenant e ruolo.
- STEP 24 completato: production readiness checklist e smoke test runbook.
- STEP 25 completato: admin cycle management base.
- STEP 26 completato: cycle participant management base.
- STEP 27 completato: manager relation admin base.
- STEP 28 completato: import esteso setup performance.
- STEP 29 completato: final hardening e deploy rehearsal base.
- STEP 30 completato: OrgUnit admin management base.
- STEP 31 completato: JobRole admin management base.
- STEP 32 completato: Org chart e struttura organizzativa read-only.
- STEP 33 completato: Manager relations admin hardening.
- STEP 34 completato: Cycle management hardening.
- STEP 35 completato: Cycle participants admin hardening.
- STEP 36 completato: Goal catalog admin.
- STEP 37 completato: Goal assignment admin.

## Prossimo step consigliato

### STEP 38 - Competency catalog admin

Obiettivo:
- gestire il catalogo competenze da UI.

Include:
- lista competenze.
- create/edit competency.
- code/name.
- descrizione.
- stato attivo/non attivo se supportato dal modello.
- blocco delete se referenziata.

---

## Roadmap estesa post prima tranche deployabile

Nota:
- lo STEP 29 non rappresenta la fine completa del prodotto.
- lo STEP 29 chiude una prima tranche deployabile e testabile.
- la piattaforma completa richiede ancora i moduli seguenti.

### STEP 30 - OrgUnit admin management

Obiettivo:
- gestire da UI le unita organizzative.
- rendere visibile e modificabile la struttura `OrgUnits`.

Include:
- lista OrgUnits.
- create/edit OrgUnit.
- parent OrgUnit.
- validazione anti-loop sulla gerarchia.
- disattivazione soft o blocco delete se referenziata.

### STEP 31 - JobRole admin management

Obiettivo:
- gestire da UI i ruoli/mansioni aziendali.

Include:
- lista JobRoles.
- create/edit JobRole.
- collegamento con employee.
- blocco delete se referenziato.

### STEP 32 - Org chart e struttura organizzativa

Obiettivo:
- visualizzare la struttura organizzativa e renderla utile anche per HR/admin.

Include:
- vista ad albero OrgUnits.
- employee per OrgUnit.
- manager principali.
- evidenza employee senza OrgUnit.

### STEP 33 - Manager relations admin

Obiettivo:
- gestire `EmployeeManagers` da UI.
- eliminare script manuali per creare subordinati sotto un manager.

Include:
- selezione employee.
- selezione manager.
- start/end date.
- primary relation.
- relationship type.
- validazioni su date e duplicati.

### STEP 34 - Cycle management completo

Obiettivo:
- portare la gestione ciclo oltre la base.

Include:
- create/edit cycle.
- template associato.
- status cycle.
- date ciclo.
- attivazione/chiusura ciclo.
- protezioni se esistono participant/goal/assessment collegati.

### STEP 35 - Cycle participants admin

Obiettivo:
- gestire partecipanti e fase corrente da UI.

Include:
- add/remove participant.
- current phase iniziale.
- status partecipazione.
- reset fase controllato.
- bulk add da employee attivi.

### STEP 36 - Goal catalog admin

Obiettivo:
- gestire catalogo goal da UI.

Include:
- create/edit goal.
- categorie o tag se utili.
- stato attivo/non attivo.
- descrizione e target metadata.

### STEP 37 - Goal assignment admin

Obiettivo:
- assegnare goal agli employee/cicli da UI.

Include:
- assegnazione goal a singolo employee.
- assegnazione bulk per ciclo.
- pesi.
- target value.
- due date.
- validazioni somma pesi dove applicabile.

### STEP 38 - Competency catalog admin

Obiettivo:
- gestire catalogo competenze da UI.

Include:
- create/edit competency.
- code/name.
- descrizione.
- stato attivo/non attivo.
- blocco delete se referenziata.

### STEP 39 - Competency model admin

Obiettivo:
- gestire i modelli competenze da UI.

Include:
- create/edit model.
- min/max score.
- righe model item.
- required.
- weight.
- ordinamento competenze.

### STEP 40 - Assessment setup admin

Obiettivo:
- configurare generazione e responsabilita assessment per ciclo.

Include:
- assessment type.
- target/evaluator.
- model associato.
- generazione assessment per participant.
- rigenerazione controllata solo se sicura.

### STEP 41 - Reporting HR base

Obiettivo:
- introdurre report operativi HR.

Include:
- stato partecipanti per ciclo/fase.
- assessment draft/submitted/closed.
- required mancanti.
- employee senza manager.
- employee senza user link.

### STEP 42 - Reporting manager base

Obiettivo:
- introdurre report per manager.

Include:
- stato team.
- goal completion/progress.
- assessment mancanti.
- target in ritardo.
- export dei dati team.

### STEP 43 - Performance dashboard aggregata

Obiettivo:
- dare una vista aggregata utile a HR e direzione.

Include:
- distribuzione score.
- avanzamento ciclo.
- andamento per OrgUnit.
- andamento per JobRole.
- KPI completamento.

### STEP 44 - Export Excel/CSV

Obiettivo:
- esportare dati principali per HR e admin.

Include:
- export employee.
- export participants.
- export goals.
- export assessments.
- export report HR.
- filtri ciclo/fase/OrgUnit/JobRole.

### STEP 45 - Import esteso performance

Obiettivo:
- completare gli import oltre employee base.

Include:
- import OrgUnits.
- import JobRoles.
- import manager relations.
- import cycle participants.
- import goal assignments.
- import competency catalog/model.
- validazioni no-partial-save.

### STEP 46 - SSO Active Directory / Entra ID readiness

Obiettivo:
- consentire single sign-on aziendale.

Include:
- analisi provider target: Active Directory on-prem, ADFS o Microsoft Entra ID.
- configurazione OpenID Connect/SAML secondo stack scelto.
- mapping username/email.
- mapping ruoli ABP da claim o gruppi AD.
- fallback admin locale.
- test login/logout.
- test tenant e ruoli.
- documentazione setup ambiente.

### STEP 47 - Role mapping e authorization hardening

Obiettivo:
- rendere robusta la gestione ruoli applicativi.

Include:
- mapping gruppi AD/Entra verso ruoli `Dipendente`, `Responsabile`, `HR`, `admin`.
- revisione policy admin.
- revisione accesso pagine.
- test negativi per ruoli non autorizzati.

### STEP 48 - Audit log e tracciamento azioni

Obiettivo:
- tracciare operazioni rilevanti.

Include:
- edit assessment item.
- submit assessment.
- advance phase.
- import dati.
- modifiche admin workflow.
- linking utenti.
- export report.

### STEP 49 - Notification/email readiness

Obiettivo:
- preparare comunicazioni operative.

Include:
- notifica apertura ciclo.
- reminder assessment mancanti.
- notifica submit completato.
- reminder manager.
- configurazione SMTP/provider.
- template email.

### STEP 50 - UX polish globale

Obiettivo:
- rifinire esperienza utente prima del go-live completo.

Include:
- testi coerenti italiano/inglese.
- loading states.
- error messages.
- empty states.
- layout mobile/desktop.
- navigazione coerente tra Self, Team, HR, Admin.

### STEP 51 - Security review

Obiettivo:
- revisione sicurezza prima del deploy definitivo.

Include:
- tenant isolation.
- role enforcement server-side.
- query target employee non manipolabili.
- manager scope via `EmployeeManagers`.
- admin endpoints protetti.
- import sanitization.
- file/export permissions.

### STEP 52 - Performance review

Obiettivo:
- verificare query e tempi sulle viste principali.

Include:
- dashboard My.
- dashboard Team.
- dashboard HR.
- Admin Employees.
- report HR.
- indici DB consigliati.
- paginazione dove necessaria.

### STEP 53 - Backup, restore e disaster recovery

Obiettivo:
- preparare procedure operative produzione.

Include:
- backup DB.
- restore test.
- retention.
- piano rollback deploy.
- script di verifica post-restore.

### STEP 54 - Deploy staging

Obiettivo:
- provare un deploy completo prima della produzione.

Include:
- configurazione ambiente staging.
- connection string.
- tenant.
- SSO se disponibile.
- smoke test completo.
- correzioni post-test.

### STEP 55 - UAT finale

Obiettivo:
- validazione con utenti reali.

Include:
- scenario employee.
- scenario manager.
- scenario HR.
- scenario admin.
- raccolta feedback.
- bugfix prioritari.

### STEP 56 - Deploy produzione

Obiettivo:
- rilascio definitivo.

Include:
- freeze versione.
- backup pre-deploy.
- deploy applicazione.
- verifica DB.
- smoke test produzione.
- piano rollback pronto.

### STEP 57 - Post go-live support

Obiettivo:
- gestire stabilizzazione dopo rilascio.

Include:
- monitoraggio errori.
- raccolta segnalazioni.
- fix prioritari.
- micro-migliorie UX.
- retrospettiva tecnica.

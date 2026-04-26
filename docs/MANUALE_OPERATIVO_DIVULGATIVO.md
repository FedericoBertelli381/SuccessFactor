# Manuale operativo e divulgativo - SuccessFactor

Data documento: 2026-04-20

## 1. Scopo del manuale

Questo manuale spiega come funziona la piattaforma SuccessFactor, quali concetti usa, quali sono i ruoli coinvolti e come svolgere le principali operazioni quotidiane.

Il documento ha due obiettivi:

- divulgativo: aiutare utenti, HR, manager e referenti di progetto a capire il senso dei concetti applicativi.
- operativo: dare istruzioni pratiche per usare, amministrare, controllare e validare il sistema.

Non e' un manuale di sviluppo codice. Per dettagli tecnici interni restano validi anche:

- `docs/PROJECT_HANDOFF.md`
- `docs/PRODUCTION_READINESS_CHECKLIST.md`
- `docs/STAGING_DEPLOY_RUNBOOK.md`
- `docs/BACKUP_RESTORE_DR.md`
- `docs/SECURITY_REVIEW.md`
- `docs/PERFORMANCE_REVIEW.md`

## 2. Che cos'e' SuccessFactor

SuccessFactor e' una piattaforma per gestire un processo di performance management aziendale.

In termini pratici consente di:

- censire dipendenti, ruoli, unita organizzative e relazioni manageriali.
- configurare cicli di valutazione.
- assegnare obiettivi ai dipendenti.
- monitorare avanzamento degli obiettivi.
- creare e compilare valutazioni basate su competenze.
- distinguere cosa puo fare un dipendente, un responsabile, HR o un amministratore.
- controllare lo stato dei processi tramite dashboard e report.
- tracciare le operazioni importanti tramite audit log.

Il sistema non e' solo una raccolta di maschere. Il cuore del prodotto e' il workflow: cioe' l'insieme di fasi, permessi e regole che determinano cosa e' modificabile, quando lo e', e da parte di chi.

## 3. Concetti fondamentali

### 3.1 Tenant

Un tenant rappresenta un perimetro aziendale o organizzativo separato.

In un'applicazione multi-tenant, dati di tenant diversi devono rimanere separati. Questo vuol dire che:

- un dipendente di un tenant non deve vedere dati di un altro tenant.
- i cicli sono filtrati per tenant.
- import, export, report e pagine admin devono lavorare nel tenant corrente.

Operativamente, molte URL usano il parametro:

```text
?__tenant=<nome-tenant>
```

Esempio:

```text
/my?__tenant=Default
```

La regola pratica e': quando si naviga tra pagine interne, il tenant deve essere preservato.

### 3.2 Utente applicativo

L'utente applicativo e' l'account con cui una persona effettua login.

L'utente puo essere:

- locale, creato nel sistema identity.
- esterno, in futuro via SSO Active Directory, ADFS o Microsoft Entra ID.

L'utente applicativo da solo non basta per usare il flusso dipendente. Serve anche il collegamento con un record Employee.

### 3.3 Employee

Employee e' il dipendente censito nel dominio HR/performance.

Contiene dati come:

- matricola.
- nome completo.
- email.
- stato attivo/non attivo.
- eventuale unita organizzativa.
- eventuale job role.
- eventuale collegamento a un utente applicativo.

Per usare la pagina `/my`, l'utente loggato deve essere collegato a un Employee. Se manca il collegamento, il sistema deve mostrare un errore business leggibile, non bloccare l'app.

Errore tipico:

```text
EmployeeNotLinkedToUser
```

Significa: l'utente esiste, ma non e' ancora associato a un dipendente.

### 3.4 Identity link

L'identity link e' il collegamento tra utente applicativo ed Employee.

Serve per rispondere alla domanda:

```text
Questo login a quale dipendente corrisponde?
```

Senza identity link:

- il dipendente non puo usare correttamente `/my`.
- il manager non puo essere riconosciuto come employee-manager.
- alcune regole di scope non possono essere applicate.

Il collegamento si gestisce dalla pagina admin:

```text
/admin/identity-link?__tenant=<tenant>
```

### 3.5 OrgUnit

OrgUnit rappresenta un'unita organizzativa.

Serve a descrivere la struttura aziendale, ad esempio:

- Direzione.
- Area HR.
- Area Operations.
- Reparto.
- Ufficio.

Le OrgUnit possono essere gerarchiche: una unita puo avere una unita padre.

Nota importante: nel sistema attuale le OrgUnit sono informative e utili per report, filtri e organigramma. Non determinano da sole lo scope manageriale.

### 3.6 JobRole

JobRole rappresenta un ruolo o mansione.

Esempi:

- HR Specialist.
- Responsabile di funzione.
- Addetto operativo.
- Project manager.

Serve per classificare gli employee, filtrare report e leggere le distribuzioni di performance per ruolo.

### 3.7 Manager relation

La relazione manageriale indica che un employee e' subordinato a un manager.

Nel sistema attuale questa e' la fonte reale dello scope manageriale.

La relazione contiene:

- employee subordinato.
- manager employee.
- tipo relazione.
- flag primary.
- data inizio.
- eventuale data fine.

Un manager vede e opera sui propri subordinati se esiste una relazione valida in `EmployeeManagers`.

Una relazione e' attiva quando:

- la data inizio e' gia passata o uguale a oggi.
- la data fine e' vuota oppure e' futura o uguale a oggi.

### 3.8 Cycle

Un cycle e' un ciclo di performance.

Esempi:

- Performance 2026.
- Valutazione annuale 2026.
- Ciclo obiettivi primo semestre.

Un ciclo contiene o collega:

- anno.
- nome.
- stato.
- template workflow.
- partecipanti.
- obiettivi.
- assessment.

Stati tipici:

- Draft: ciclo preparatorio.
- Active: ciclo attivo.
- Closed: ciclo chiuso.

### 3.9 Cycle participant

Un cycle participant e' un employee inserito in un ciclo.

Non tutti gli employee devono necessariamente partecipare a ogni ciclo. Il participant dice:

```text
Questo dipendente partecipa a questo ciclo.
```

Contiene:

- ciclo.
- employee.
- fase corrente.
- status partecipazione.

Status tipici:

- Active.
- Completed.
- Excluded.

### 3.10 Process template

Il process template e' il modello di workflow.

Descrive la sequenza e la logica del processo. Ad esempio:

- autovalutazione dipendente.
- valutazione manager.
- revisione HR.
- chiusura.

Il template non e' il ciclo: il template e' il modello, il ciclo e' l'istanza concreta per un periodo.

### 3.11 Process phase

La phase e' una fase del workflow.

Esempi:

- SELF_ASSESSMENT.
- MGR_ASSESSMENT.
- HR_REVIEW.
- CLOSED.

Ogni participant puo trovarsi in una fase corrente. La fase influenza:

- quali azioni sono disponibili.
- quali campi sono modificabili.
- chi puo avanzare il processo.
- chi puo fare submit.

### 3.12 Phase transition

La transition indica come si passa da una fase a un'altra.

Esempio:

```text
SELF_ASSESSMENT -> MGR_ASSESSMENT
```

Il passaggio di fase deve rispettare regole e permessi. Non deve essere solo un cambio manuale libero.

### 3.13 Phase role permission

Una phase role permission dice cosa puo fare un ruolo in una fase.

Esempi di permessi:

- puo leggere.
- puo modificare.
- puo fare submit.
- puo avanzare fase.

Questo e' importante perche evita di codificare regole rigide solo nella UI. La UI puo nascondere o disabilitare pulsanti, ma il server deve comunque controllare i permessi.

### 3.14 Phase field policy

Una phase field policy decide il livello di accesso a un campo in una fase per un ruolo.

Valori tipici:

- Hidden: il campo non deve essere visibile.
- Read: il campo e' visibile ma non modificabile.
- Edit: il campo e' modificabile.

Esempio:

```text
Campo Score, fase MGR_ASSESSMENT, ruolo Responsabile: Edit
```

Significa che il manager puo modificare lo score in quella fase.

### 3.15 Goal catalog

Il goal catalog e' il catalogo degli obiettivi disponibili.

Un goal di catalogo e' una definizione riutilizzabile. Puo contenere:

- titolo.
- descrizione.
- categoria.
- peso predefinito.
- flag attivo/libreria.

Il catalogo aiuta a standardizzare gli obiettivi.

### 3.16 Goal assignment

Il goal assignment e' l'assegnazione concreta di un obiettivo a un employee in un ciclo.

Contiene:

- employee.
- ciclo.
- goal.
- peso.
- target value.
- date.
- stato.

Esempio:

```text
Obiettivo "Ridurre tempi di lavorazione" assegnato a Mario Rossi nel ciclo Performance 2026.
```

### 3.17 Goal progress

Il goal progress e' un aggiornamento di avanzamento su un obiettivo assegnato.

Serve a registrare:

- percentuale o avanzamento.
- nota.
- data.
- autore.

In questo modo l'obiettivo non e' solo una riga statica, ma ha uno storico.

### 3.18 Competency catalog

Il competency catalog e' il catalogo delle competenze.

Esempi:

- Comunicazione.
- Leadership.
- Problem solving.
- Orientamento al cliente.

Una competenza puo essere attiva o non attiva. Se e' gia usata da modelli o assessment, la cancellazione deve essere bloccata.

### 3.19 Competency model

Il competency model e' un modello di valutazione composto da piu competenze.

Contiene:

- nome modello.
- scala punteggio minima/massima.
- lista competenze.
- required.
- peso.
- ordinamento.

Serve a dire quali competenze valutare e con quale scala.

### 3.20 Competency assessment

Il competency assessment e' una valutazione concreta.

Esempi:

- autovalutazione del dipendente.
- valutazione manager.

Contiene:

- ciclo.
- employee valutato.
- eventuale evaluator.
- tipo assessment.
- status.
- righe assessment item.

Status tipici:

- Draft: modificabile se permesso.
- Submitted: inviato, non piu modificabile.
- Closed: chiuso.

### 3.21 Competency assessment item

L'assessment item e' la singola riga di competenza dentro un assessment.

Contiene:

- competenza.
- score.
- commento.
- required.
- peso.

Il submit di un assessment deve verificare che gli item required abbiano lo score valorizzato.

### 3.22 Audit log

L'audit log registra eventi business importanti.

Esempi:

- modifica assessment.
- submit assessment.
- avanzamento fase.
- import dati.
- export dati.
- modifiche admin.

Serve a ricostruire cosa e' successo, quando, su quale entita e da quale utente.

## 4. Ruoli applicativi

### 4.1 Dipendente

Il dipendente usa il sistema per:

- vedere i propri obiettivi.
- aggiornare avanzamenti se consentito.
- vedere o compilare le proprie valutazioni.
- fare submit quando i dati sono completi.

Area principale:

```text
/my?__tenant=<tenant>
```

### 4.2 Responsabile

Il responsabile usa il sistema per:

- vedere i subordinati.
- selezionare un target.
- consultare obiettivi e assessment del target.
- aggiornare avanzamenti o valutazioni se la fase lo consente.
- fare submit o avanzare fase quando permesso.
- consultare report manager.

Area principale:

```text
/team?__tenant=<tenant>
```

Report:

```text
/team/reports?__tenant=<tenant>
```

### 4.3 HR

HR usa il sistema per:

- monitorare i cicli.
- selezionare ciclo e dipendente.
- consultare obiettivi e assessment in sola lettura.
- usare report operativi.
- usare dashboard aggregate.
- analizzare anomalie come employee senza manager o senza user link.

Area principale:

```text
/hr?__tenant=<tenant>
```

Report:

```text
/hr/reports?__tenant=<tenant>
```

Dashboard:

```text
/hr/performance-dashboard?__tenant=<tenant>
```

### 4.4 Admin

L'amministratore configura e controlla il sistema.

Puo gestire:

- employee.
- identity link.
- org unit.
- job role.
- manager relations.
- cicli.
- participants.
- workflow.
- cataloghi goal/competenze.
- assegnazioni.
- assessment setup.
- import.
- readiness.
- audit.
- notifiche.

Le voci Admin devono essere visibili solo agli utenti autorizzati. Non basta negare l'accesso dopo il click: per chi non e' autorizzato la voce deve sparire dal menu.

## 5. Flusso Dipendente

### 5.1 Accesso alla dashboard My

URL:

```text
/my?__tenant=<tenant>
```

La dashboard mostra:

- contesto del dipendente.
- ciclo corrente o rilevante.
- fase corrente.
- obiettivi.
- assessment.
- azioni disponibili.

Se l'utente non e' collegato a un employee, compare un messaggio di errore business.

### 5.2 Consultare obiettivi

Dalla dashboard My il dipendente vede gli obiettivi assegnati.

Per ogni obiettivo e' importante controllare:

- titolo.
- peso.
- stato.
- target.
- scadenza.
- avanzamento.

### 5.3 Aprire dettaglio obiettivo

Nel dettaglio obiettivo il dipendente puo:

- leggere descrizione e dati principali.
- vedere storico avanzamenti.
- aggiungere progress se la fase e i permessi lo consentono.

Se il workflow non consente modifica, la pagina deve rimanere leggibile ma non editabile.

### 5.4 Consultare assessment

La lista assessment mostra le valutazioni collegate al dipendente.

Ogni assessment ha:

- tipo.
- stato.
- eventuale evaluator.
- righe competenza.

### 5.5 Compilare assessment

Se l'assessment e' in Draft e la fase consente edit:

- valorizzare gli score richiesti.
- inserire commenti se previsti.
- salvare le modifiche.

Se una competenza e' required, lo score e' obbligatorio prima del submit.

### 5.6 Submit assessment

Il submit invia la valutazione.

Dopo il submit:

- l'assessment passa a Submitted.
- la UI diventa read-only.
- ulteriori modifiche devono essere bloccate anche lato server.

## 6. Flusso Responsabile

### 6.1 Accesso alla dashboard Team

URL:

```text
/team?__tenant=<tenant>
```

La dashboard Team mostra i subordinati del manager corrente.

Il perimetro non deriva da OrgUnit, ma da `EmployeeManagers`.

### 6.2 Selezione target

Il responsabile seleziona un employee target.

La navigazione usa:

```text
targetEmployeeId=<id>
```

Questo parametro deve essere preservato quando si entra e si esce dai dettagli.

### 6.3 Obiettivi del target

Il responsabile puo vedere gli obiettivi del target.

A seconda del workflow puo:

- solo leggere.
- aggiungere avanzamenti.
- modificare dati specifici.

La regola deve essere determinata dal workflow, non da assunzioni hardcoded.

### 6.4 Assessment del target

Il responsabile puo aprire assessment del target.

Se e' evaluator e la fase lo consente, puo:

- modificare score.
- modificare commenti.
- usare salvataggio multiplo.
- fare submit.

Se ci sono righe sporche non salvate, il sistema deve evitare submit incoerenti.

### 6.5 Report manager

URL:

```text
/team/reports?__tenant=<tenant>
```

Il report manager serve a vedere:

- stato del team.
- participant per fase.
- goal per stato.
- goal senza progress o in ritardo.
- assessment mancanti.
- assessment con required mancanti.

## 7. Flusso HR

### 7.1 Dashboard HR

URL:

```text
/hr?__tenant=<tenant>
```

HR seleziona:

- ciclo.
- employee target.

Poi consulta goals e assessments del target in sola lettura.

HR non deve usare le pagine Team come scorciatoia, perche il ruolo HR ha un contesto diverso da quello manageriale.

### 7.2 Dettagli HR

HR ha pagine detail dedicate per:

- goal target.
- assessment target.

Queste pagine preservano:

- tenant.
- cycleId.
- targetEmployeeId.

### 7.3 Report HR

URL:

```text
/hr/reports?__tenant=<tenant>
```

Il report HR aiuta a monitorare:

- participant per fase/status.
- assessment per stato/tipo.
- required mancanti.
- participant attivi senza manager attivo.
- participant attivi senza user link.

### 7.4 Performance dashboard

URL:

```text
/hr/performance-dashboard?__tenant=<tenant>
```

La dashboard aggregata mostra:

- KPI ciclo.
- avanzamento participant.
- progress medio goal.
- completion assessment.
- score medio.
- distribuzione score.
- breakdown per OrgUnit.
- breakdown per JobRole.

Serve per una lettura direzionale, non per modificare dati.

## 8. Area Admin

### 8.1 Readiness

URL:

```text
/admin/readiness?__tenant=<tenant>
```

Serve per controllare lo stato minimo del sistema prima e dopo deploy.

Controlla elementi come:

- tenant.
- ruolo admin.
- workflow template.
- fasi.
- policy.
- cicli attivi.
- participant.
- user link.
- manager relations.
- assessment.

Usare questa pagina come controllo rapido prima di aprire UAT o staging agli utenti.

### 8.2 Employees

URL:

```text
/admin/employees?__tenant=<tenant>
```

Serve per:

- creare employee.
- modificare dati base.
- verificare collegamento utente.
- associare OrgUnit e JobRole.
- importare employee da CSV.

Regole operative:

- la matricola deve essere stabile.
- email deve essere coerente con l'utente identity se si vuole usare link by email.
- gli employee non attivi non dovrebbero partecipare a nuovi cicli salvo eccezioni.

### 8.3 Identity link

URL:

```text
/admin/identity-link?__tenant=<tenant>
```

Serve per collegare utenti ABP e dipendenti.

Operazioni:

- cercare utenti.
- collegare employee a user.
- scollegare mapping.
- tentare link automatico via email.

Controllo fondamentale:

- ogni dipendente che deve usare `/my` deve avere `UserId` valorizzato.

### 8.4 OrgUnits

URL:

```text
/admin/org-units?__tenant=<tenant>
```

Serve per gestire la struttura organizzativa.

Operazioni:

- creare unita.
- modificare nome.
- assegnare parent.
- evitare loop gerarchici.
- bloccare delete se usata da employee o figli.

### 8.5 Org chart

URL:

```text
/admin/org-chart?__tenant=<tenant>
```

Vista read-only della struttura organizzativa.

Mostra:

- gerarchia OrgUnit.
- employee diretti.
- job role.
- manager primario attivo.
- employee senza OrgUnit.

### 8.6 JobRoles

URL:

```text
/admin/job-roles?__tenant=<tenant>
```

Serve per gestire ruoli/mansioni.

Regole:

- evitare duplicati.
- bloccare delete se il ruolo e' usato da employee.
- usare nomi chiari e coerenti.

### 8.7 Manager relations

URL:

```text
/admin/manager-relations?__tenant=<tenant>
```

Serve per amministrare la catena manageriale.

Operazioni:

- creare relazione employee-manager.
- indicare primary.
- indicare tipo relazione.
- impostare start/end date.
- chiudere relazione.
- filtrare relazioni attive, future o chiuse.

Questa pagina e' cruciale per il funzionamento della dashboard Team.

### 8.8 Cycles

URL:

```text
/admin/cycles?__tenant=<tenant>
```

Serve per gestire cicli performance.

Operazioni:

- creare ciclo.
- modificare dati.
- associare template.
- attivare ciclo.
- chiudere ciclo.

Attenzione:

- chiudere un ciclo deve bloccare o limitare modifiche successive.
- cambiare template dopo setup avanzato puo essere rischioso.

### 8.9 Cycle participants

URL:

```text
/admin/cycle-participants?__tenant=<tenant>
```

Serve per decidere chi partecipa a un ciclo.

Operazioni:

- aggiungere participant.
- rimuovere participant se consentito.
- modificare fase corrente.
- modificare status.
- bulk add employee attivi mancanti.
- reset controllato fase.

### 8.10 Workflow

URL:

```text
/admin/workflow?__tenant=<tenant>
```

Serve per configurare:

- template.
- fasi.
- role permissions.
- field policies.

E' una delle aree piu delicate.

Regola pratica:

- ogni modifica workflow va testata su dati di prova prima di usarla su ciclo reale.

### 8.11 Goal catalog

URL:

```text
/admin/goal-catalog?__tenant=<tenant>
```

Serve per gestire obiettivi riutilizzabili.

Operazioni:

- creare goal.
- modificare descrizione/categoria/peso.
- disattivare se non piu utilizzabile.
- bloccare delete se gia assegnato.

### 8.12 Goal assignments

URL:

```text
/admin/goal-assignments?__tenant=<tenant>
```

Serve per assegnare obiettivi ai participant.

Regole:

- goal e participant devono appartenere al ciclo corretto.
- evitare duplicati.
- controllare peso massimo complessivo.
- evitare modifiche su cicli chiusi.

### 8.13 Competency catalog

URL:

```text
/admin/competency-catalog?__tenant=<tenant>
```

Serve per gestire il catalogo competenze.

Regole:

- code e name devono essere chiari.
- non cancellare competenze usate da modelli o assessment.
- disattivare invece di cancellare quando ci sono storici.

### 8.14 Competency models

URL:

```text
/admin/competency-models?__tenant=<tenant>
```

Serve per comporre modelli valutativi.

Operazioni:

- creare modello.
- definire scala punteggio.
- aggiungere competenze.
- indicare required.
- indicare peso.

Regole:

- evitare duplicati competenza nello stesso modello.
- controllare somma pesi.
- evitare modifiche strutturali se il modello e' gia usato da assessment.

### 8.15 Assessment setup

URL:

```text
/admin/assessment-setup?__tenant=<tenant>
```

Serve per generare assessment.

Concetto operativo:

- si seleziona ciclo/participant/evaluator/tipo/model.
- il sistema crea assessment e righe item dal competency model.

Regole:

- non rigenerare assessment Submitted o Closed.
- su Draft, rigenerare solo aggiungendo item mancanti senza sovrascrivere score/commenti.

### 8.16 Performance import

URL:

```text
/admin/performance-import?__tenant=<tenant>
```

Serve per importare dati di setup performance.

Import gestiti:

- cycle participants.
- manager relations.

Regola fondamentale:

- no partial save.

Significa che se una riga contiene errore, nessuna riga deve essere salvata.

### 8.17 Audit

URL:

```text
/admin/audit?__tenant=<tenant>
```

Serve per cercare eventi business.

Filtri:

- data.
- utente.
- azione.
- entita.
- tenant.

Uso tipico:

- capire chi ha fatto submit.
- capire chi ha modificato dati.
- verificare export.
- analizzare anomalie operative.

### 8.18 Notifications

URL:

```text
/admin/notifications?__tenant=<tenant>
```

Serve per preparare e verificare notifiche email.

Stato attuale:

- configurazione SMTP prevista.
- test end-to-end SMTP non ancora completato per vincoli esterni.
- test completo da ricordare in UAT finale.

## 9. Import ed export

### 9.1 Import employee

Formato:

```csv
Matricola;FullName;Email;OrgUnit;JobRole;IsActive
M011;Catia Esposito;c.esposito@example.local;;;true
M012;Mario Rossi;m.rossi@example.local;-;-;true
```

Regole:

- la matricola identifica l'employee.
- OrgUnit e JobRole possono essere vuoti se non disponibili.
- se una riga e' errata, l'import deve bloccare tutto.

### 9.2 Import participants

Formato:

```csv
Cycle;EmployeeMatricola;PhaseCode;Status
Performance 2026;M011;MGR_ASSESSMENT;Active
Performance 2026;M012;-;Active
```

Regole:

- il ciclo deve esistere.
- la matricola deve esistere.
- la phase code deve esistere se valorizzata.
- status deve essere coerente.

### 9.3 Import manager relations

Formato:

```csv
EmployeeMatricola;ManagerMatricola;RelationType;IsPrimary;StartDate;EndDate
M011;M001;Line;true;2026-01-01;
M012;M001;Line;true;2026-01-01;
```

Regole:

- employee e manager devono esistere.
- un employee non puo essere manager di se stesso.
- date devono essere coerenti.
- evitare duplicati.

### 9.4 Export

Gli export CSV servono a estrarre dati per controlli HR/admin.

Prima di esportare:

- scegliere ciclo corretto.
- applicare filtri quando disponibili.
- ricordare che un export puo contenere dati personali.

Dopo export:

- conservare il file solo dove autorizzato.
- non inviare via canali non protetti.
- usare audit log per tracciare operazioni importanti.

## 10. Sicurezza e autorizzazioni

### 10.1 Menu e accesso

Le voci menu devono rispettare i ruoli.

Regola:

- se un utente non e' autorizzato, la voce non deve apparire.
- se prova comunque a raggiungere l'URL, il server deve bloccare l'accesso.

Quindi ci sono due livelli:

- UX: voce nascosta.
- sicurezza reale: controllo server-side.

### 10.2 Tenant isolation

Ogni query operativa deve rispettare il tenant.

Segnale di problema:

- dati mancanti senza motivo.
- dati di altri tenant visibili.
- navigazioni che perdono `?__tenant=`.

### 10.3 Manager scope

Il manager puo operare solo sui subordinati derivati da `EmployeeManagers`.

Se tenta un target fuori scope, deve ricevere errore business.

Errore tipico:

```text
TargetEmployeeNotInManagerScope
```

### 10.4 Errori business

Gli errori business devono essere mostrati in modo leggibile nel banner rosso, senza bloccare l'esecuzione dell'app.

Esempi:

- EmployeeNotLinkedToUser.
- TargetEmployeeNotInManagerScope.
- assessment non modificabile.
- permesso workflow mancante.

## 11. Notifiche email

La piattaforma prevede notifiche email per eventi operativi.

Esempi futuri:

- apertura ciclo.
- reminder assessment mancanti.
- conferma submit.
- reminder manager.

Stato operativo attuale:

- provider SMTP configurabile.
- invio end-to-end non ancora validato completamente per vincoli del relay.
- test SMTP finale rimandato a UAT.

Problema gia incontrato:

```text
Relay access denied
```

Significa che il server SMTP rifiuta l'invio verso quel destinatario o da quella sorgente. Serve autorizzazione lato relay o configurazione DBA/sistemistica/mail.

## 12. SSO

SSO significa Single Sign-On: l'utente entra usando identita aziendale.

Provider possibili:

- Active Directory on-prem con ADFS.
- Microsoft Entra ID.
- altro provider OpenID Connect compatibile.

Stato operativo attuale:

- predisposizione OpenID Connect presente.
- mapping claim/ruoli previsto.
- test end-to-end rimandato per disponibilita provider.

Il test SSO deve verificare:

- login.
- logout.
- email/username.
- ruoli.
- tenant.
- fallback admin locale.

## 13. Backup, restore e disaster recovery

La procedura e' documentata in:

```text
docs/BACKUP_RESTORE_DR.md
```

Concetto base:

- backup serve a poter tornare indietro.
- restore test serve a dimostrare che il backup e' davvero utilizzabile.
- retention decide per quanto tempo si conservano i backup.
- rollback deploy decide come tornare a una versione precedente.

Vincolo attuale:

- sul server di test non ci sono permessi backup per l'utente applicativo.
- al momento DBA non disponibile.

Decisione:

- non bloccare staging.
- prima della produzione, ottenere procedura backup/restore con DBA o sistemista.

## 14. Deploy staging

La procedura e' documentata in:

```text
docs/STAGING_DEPLOY_RUNBOOK.md
```

Concetto:

- staging e' un ambiente di prova completo.
- deve assomigliare alla produzione.
- serve a scoprire problemi prima degli utenti finali.

Elementi minimi:

- URL HTTPS.
- database staging.
- configurazione `ASPNETCORE_ENVIRONMENT=Staging`.
- connection string staging.
- file `appsettings.Staging.json` non versionato.
- publish Release.
- smoke test.

Template disponibili:

- `src/SuccessFactor.Blazor/appsettings.Staging.example.json`
- `src/SuccessFactor.DbMigrator/appsettings.Staging.example.json`

Script:

```powershell
.\etc\scripts\step-54-publish-staging.ps1
```

## 15. Smoke test essenziali

### 15.1 Test login

Verificare login per:

- admin.
- dipendente.
- manager.
- HR.

### 15.2 Test My

URL:

```text
/my?__tenant=<tenant>
```

Controlli:

- dashboard carica.
- goals visibili.
- detail goal apribile.
- progress aggiungibile se permesso.
- assessment visibili.
- assessment detail apribile.
- save funziona se Draft.
- submit funziona solo con required valorizzati.
- dopo submit la UI e' read-only.

### 15.3 Test Team

URL:

```text
/team?__tenant=<tenant>
```

Controlli:

- subordinati visibili.
- target selezionabile.
- query `targetEmployeeId` preservata.
- goals target visibili.
- assessment target visibili.
- save all funziona se permesso.
- submit bloccato se righe sporche o required mancanti.
- manager fuori scope bloccato.

### 15.4 Test HR

URL:

```text
/hr?__tenant=<tenant>
```

Controlli:

- ciclo selezionabile.
- target selezionabile.
- goals read-only.
- assessments read-only.
- detail HR preserva cycleId, targetEmployeeId e tenant.
- report HR apre.
- dashboard aggregata apre.

### 15.5 Test Admin

Controlli:

- menu Admin visibile solo ad admin.
- readiness senza errori inattesi.
- identity link funziona.
- employee create/edit funziona.
- manager relation create/edit/end funziona.
- cycle participant create/edit funziona.
- workflow config leggibile e modificabile con cautela.
- audit filtrabile.

## 16. Troubleshooting

### 16.1 Utente admin clicca My e vede errore EmployeeNotLinkedToUser

Causa:

- l'utente admin non e' collegato a un Employee.

Soluzione:

- se admin deve usare anche `/my`, collegarlo a un employee.
- se admin non deve usare `/my`, considerare normale l'errore business.

### 16.2 Manager non vede subordinati

Possibili cause:

- manca relazione in `EmployeeManagers`.
- relazione ha `EndDate` nel passato.
- manager non e' collegato a Employee.
- tenant errato.

Soluzione:

- controllare `/admin/manager-relations`.
- controllare identity link.
- controllare parametro `?__tenant=`.

### 16.3 HR vede dati vuoti

Possibili cause:

- ciclo non selezionato.
- ciclo senza participant.
- tenant errato.
- employee non incluso nel ciclo.

Soluzione:

- controllare `/admin/cycles`.
- controllare `/admin/cycle-participants`.
- controllare report HR.

### 16.4 Menu Admin non visibile

Possibili cause:

- utente non ha ruolo admin.
- mapping SSO/ruoli non corretto.
- tenant/utente errato.

Soluzione:

- controllare ruoli utente.
- controllare configurazione role mapping.
- verificare che la voce non debba comparire per quel profilo.

### 16.5 Errore SMTP Relay access denied

Causa:

- relay SMTP non autorizza il mittente, destinatario o server applicativo.

Soluzione:

- chiedere autorizzazione relay.
- verificare FromAddress.
- verificare porta e SSL.
- ripetere test end-to-end in UAT.

### 16.6 Tabella audit mancante

Errore tipico:

```text
Invalid object name 'dbo.BusinessAuditEvents'
```

Causa:

- script/migrazione audit non applicata.

Soluzione:

- applicare `etc/scripts/step-48-create-business-audit-events.sql` se necessario.
- verificare DbMigrator/migrazioni.

### 16.7 App si blocca su errore business

Comportamento atteso:

- l'app non deve bloccarsi.
- deve mostrare banner rosso con messaggio business leggibile.

Se si blocca:

- verificare gestione eccezioni UI.
- verificare che il service usi status/context quando opportuno invece di propagare eccezioni non gestite.

## 17. Checklist operativa giornaliera

Controlli semplici:

- login admin funziona.
- readiness non mostra errori inattesi.
- ciclo attivo presente.
- employee principali collegati a user.
- manager principali hanno subordinati.
- audit log registra eventi recenti.
- nessun errore applicativo bloccante.

## 18. Checklist prima di UAT

Prima di aprire agli utenti:

- build OK.
- deploy staging OK.
- tenant corretto.
- dati di test coerenti.
- utenti employee/manager/HR/admin disponibili.
- identity link completati.
- manager relations attive.
- cycle participants configurati.
- goals assegnati.
- assessment generati.
- readiness OK o warning accettati.
- test SSO pianificato.
- test SMTP pianificato.
- vincolo backup DBA tracciato.

## 19. Checklist prima di produzione

Prima della produzione:

- backup DB disponibile.
- restore test eseguito almeno una volta.
- piano rollback applicativo pronto.
- piano rollback DB discusso.
- deploy staging validato.
- UAT completata.
- test SSO end-to-end completato.
- test SMTP end-to-end completato o rischio accettato formalmente.
- vulnerabilita pacchetti valutate.
- utenti e ruoli validati.
- menu e permessi verificati.
- audit log attivo.

## 20. Glossario rapido

Tenant:

- perimetro dati separato.

User:

- account di login.

Employee:

- dipendente censito nel processo performance.

Identity link:

- collegamento tra user ed employee.

OrgUnit:

- unita organizzativa.

JobRole:

- mansione o ruolo aziendale.

Manager relation:

- relazione employee-manager che determina lo scope del responsabile.

Cycle:

- ciclo di performance.

Participant:

- employee incluso in un ciclo.

Workflow:

- regole del processo.

Phase:

- fase del workflow.

Permission:

- cosa puo fare un ruolo in una fase.

Field policy:

- visibilita/modificabilita di un campo in una fase.

Goal:

- obiettivo.

Goal assignment:

- obiettivo assegnato a un employee in un ciclo.

Goal progress:

- aggiornamento di avanzamento.

Competency:

- competenza valutabile.

Competency model:

- insieme di competenze e regole di valutazione.

Assessment:

- valutazione concreta.

Audit:

- tracciamento eventi business.

Readiness:

- controllo stato minimo prima di test/deploy.

UAT:

- test finale con utenti reali.

## 21. Regole d'oro

- Non modificare workflow su dati reali senza test.
- Non avviare produzione senza backup e restore test.
- Non considerare la UI come unica sicurezza: il server deve sempre controllare.
- Non usare OrgUnit come scope manageriale finche il modello non viene esteso.
- Non perdere il parametro `?__tenant=`.
- Non cancellare dati referenziati: disattivare quando serve mantenere storico.
- Non importare dati massivi senza prima validare CSV e tenant.
- Non ignorare utenti senza identity link.
- Non aprire UAT senza smoke test base completato.

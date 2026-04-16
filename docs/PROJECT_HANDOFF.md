# SuccessFactor Project - PROJECT_HANDOFF

## 1. Scopo del progetto

Clone semplificato di SAP SuccessFactors Performance Management, con focus su:

- workflow performance per ciclo
- goal management
- competency/assessment management
- ruoli e permessi per fase
- esperienza self-service employee
- esperienza manageriale sui subordinati
- estensione futura verso HR/admin/import/go-live polished

---

## 2. Stack tecnico

- .NET 10
- ABP Framework
- EF Core
- Blazor
- SQL Server
- Mapperly

Contesto reale di lavoro:
- sviluppo storico su Windows Server 2022 con Visual Studio 2026
- sviluppo futuro consigliato su PC locale Windows 11
- repository Git inizializzato e pushato su GitHub
- uso futuro previsto di Codex app sulla root del progetto

---

## 3. Principi architetturali già decisi

### 3.1 Layer user-facing separati per contesto

Esistono due assi distinti:

#### Self flow
Prefisso `My*`
- `MyDashboardAppService`
- `MyGoalsAppService`
- `MyAssessmentsAppService`

Scopo:
- esperienza del current user come employee/self
- usare il contesto del current user
- non esporre dettagli workflow inutili alla UI

#### Manager flow
Prefisso `Team*`
- `TeamDashboardAppService`
- `TeamGoalsAppService`
- `TeamAssessmentsAppService`

Scopo:
- esperienza del manager sui subordinati
- contesto actor/target separato
- scope manageriale basato su `EmployeeManagers`

### 3.2 Non mischiare self e manager
Regola fondamentale:
- `My*` resta self-only
- `Team*` resta manager-only

Non bisogna “allargare” `MyWorkflowContextResolver` per usarlo anche sul target manageriale.
Per il manager flow è stato introdotto un contesto dedicato.

### 3.3 Service tecnici separati dal layer user-facing
Il layer user-facing orchestra e prepara DTO/UI-ready, ma la logica tecnica vera resta nei service dedicati.

Esempi:
- `GoalProgressAppService`
- `CompetencyAssessmentAppService`
- `CycleWorkflowAppService`

### 3.4 Workflow e field policies sono la fonte di verità
Permessi e accessi non vanno indovinati in UI.
La fonte di verità è il workflow:

- `PhaseRolePermissions`
- `PhaseFieldPolicies`
- `WorkflowAuthorizationService`
- `IPhasePermissionResolver`

### 3.5 Tenant sempre preservato nella navigazione
Regola già emersa e già risolta:
- tutte le navigazioni interne tra pagine Blazor devono preservare `?__tenant=...`

### 3.6 Evitare HttpClient raw in UI
Decisione già presa:
- niente `HttpClient` custom/raw per goal progress o assessment item
- usare proxy/service ABP già definiti

---

## 4. Modello concettuale attuale

## 4.1 Employee self
L’utente loggato entra in `/my` e lavora sui propri:
- goal
- assessment
- advance phase

## 4.2 Manager
Il manager entra in `/team` e lavora sui subordinati:
- selezione target
- lettura contesto target
- goals del target
- assessments del target
- detail goal/assessment del target
- add progress goal del target
- advance phase del target
- submit assessment del target
- edit item assessment del target

## 4.3 Scope manageriale reale
Attualmente il perimetro manageriale **non** deriva dalle OrgUnit.

La fonte reale dello scope manageriale è:
- tabella `EmployeeManagers`

Quindi:
- manager -> subordinati = `EmployeeManagers`
- non usare oggi `OrgUnit` come fonte di autorizzazione manageriale

Nota importante:
- `Employees` ha `OrgUnitId`
- il `DbContext` espone `OrgUnits`
- ma non esiste ancora una relazione manager -> org unit sufficiente a modellare il perimetro manageriale attuale

Conclusione:
- oggi lo scope manageriale è solo `EmployeeManagers`
- in futuro si potrà estendere alle OrgUnit, ma non ora

---

## 5. File e componenti chiave già presenti

## 5.1 Self flow

### Contracts
- `src/SuccessFactor.Application.Contracts/My/IMyGoalsAppService.cs`
- `src/SuccessFactor.Application.Contracts/My/IMyAssessmentsAppService.cs`
- `src/SuccessFactor.Application.Contracts/My/IMyDashboardAppService.cs`

### Application
- `src/SuccessFactor.Application/My/MyGoalsAppService.cs`
- `src/SuccessFactor.Application/My/MyAssessmentsAppService.cs`
- `src/SuccessFactor.Application/My/MyDashboardAppService.cs`

### Support self
- `src/SuccessFactor.Application/My/Support/IMyWorkflowContextResolver.cs`
- `src/SuccessFactor.Application/My/Support/MyWorkflowContext.cs`
- `src/SuccessFactor.Application/My/Support/MyWorkflowContextResolver.cs`
- `src/SuccessFactor.Application/My/Support/IPhasePermissionResolver.cs`
- `src/SuccessFactor.Application/My/Support/PhasePermissionResolver.cs`

### UI Blazor self
- `src/SuccessFactor.Blazor/Pages/My/My.razor`
- `src/SuccessFactor.Blazor/Pages/My/GoalDetail.razor`
- `src/SuccessFactor.Blazor/Pages/My/GoalDetail.razor.cs`
- `src/SuccessFactor.Blazor/Pages/My/AssessmentDetail.razor`
- `src/SuccessFactor.Blazor/Pages/My/AssessmentDetail.razor.cs`

## 5.2 Manager flow

### Contracts
- `src/SuccessFactor.Application.Contracts/Team/ITeamDashboardAppService.cs`
- `src/SuccessFactor.Application.Contracts/Team/ITeamGoalsAppService.cs`
- `src/SuccessFactor.Application.Contracts/Team/ITeamAssessmentsAppService.cs`
- `src/SuccessFactor.Application.Contracts/Team/GetTeamDashboardInput.cs`
- `src/SuccessFactor.Application.Contracts/Team/GetTeamGoalsInput.cs`
- `src/SuccessFactor.Application.Contracts/Team/GetTeamAssessmentsInput.cs`
- `src/SuccessFactor.Application.Contracts/Team/TeamDashboardDto.cs`
- `src/SuccessFactor.Application.Contracts/Team/TeamMemberDto.cs`
- `src/SuccessFactor.Application.Contracts/Team/UpdateTeamAssessmentItemDto.cs`

### Application
- `src/SuccessFactor.Application/Team/TeamDashboardAppService.cs`
- `src/SuccessFactor.Application/Team/TeamGoalsAppService.cs`
- `src/SuccessFactor.Application/Team/TeamAssessmentsAppService.cs`

### Support manager
- `src/SuccessFactor.Application/Team/Support/IManagerScopeResolver.cs`
- `src/SuccessFactor.Application/Team/Support/ManagerScopeResolver.cs`
- `src/SuccessFactor.Application/Team/Support/ITeamWorkflowContextResolver.cs`
- `src/SuccessFactor.Application/Team/Support/TeamWorkflowContext.cs`
- `src/SuccessFactor.Application/Team/Support/TeamWorkflowContextResolver.cs`

### UI Blazor manager
- `src/SuccessFactor.Blazor/Pages/Team/Team.razor`
- `src/SuccessFactor.Blazor/Pages/Team/TeamGoalDetail.razor`
- `src/SuccessFactor.Blazor/Pages/Team/TeamAssessmentDetail.razor`

## 5.3 Service tecnici già in uso
- `src/SuccessFactor.Application/Goals/GoalProgressAppService.cs`
- `src/SuccessFactor.Application/Competency/Assessment/CompetencyAssessmentAppService.cs`
- `src/SuccessFactor.Application/Cycles/CycleWorkflowAppService.cs`

### Contracts tecnici già in uso
- `src/SuccessFactor.Application.Contracts/Goals/IGoalProgressAppService.cs`
- `src/SuccessFactor.Application.Contracts/Goals/Dtos/AddGoalProgressDto.cs`
- DTO GoalProgress vari
- DTO assessment vari

---

## 6. Stato funzionale già raggiunto

## 6.1 Self flow completato in modo consistente

### Dashboard `/my`
- pagina self attiva
- carica dashboard + goals + assessments
- preserve tenant
- pulsante `Advance phase` funzionante

### Goal self
- lista goals
- detail self
- add progress funzionante
- history / summary / last progress funzionanti

### Assessment self
- lista assessments
- detail self
- save della prima riga competenza funzionante
- submit funzionante
- post-submit la UI diventa read-only

## 6.2 Manager flow già molto avanzato

### `/team`
- pagina team funzionante
- legge il manager corrente
- risolve i subordinati dal perimetro `EmployeeManagers`
- seleziona un target
- aggiorna il contesto target

### Goals target
- lista goals del target in read-only
- detail goal target dedicato
- add progress sul goal del target funzionante per il manager quando consentito

### Assessments target
- lista assessments del target in read-only
- detail assessment target dedicato
- submit assessment del target funzionante
- edit item assessment del target implementato (step 13 compilato)

### Advance phase target
- manager può fare `Advance phase` sul target quando consentito
- il target selezionato viene preservato in query string con `targetEmployeeId`

---

## 7. Dettagli importanti sul comportamento attuale

## 7.1 Stati assessment
Gestiti con:
- `Draft`
- `Submitted`
- `Closed`

Regola già implementata:
- edit item solo in `Draft`
- submit solo in `Draft`
- dopo submit: UI read-only
- server blocca edit post-submit

## 7.2 Submit assessment
Submit richiede:
- permesso di workflow `CanSubmit`
- tutte le competenze required con score valorizzato

## 7.3 Goal progress
Add progress usa:
- `IGoalProgressAppService`
- `AddGoalProgressDto`

Il dettaglio goal self e team usa:
- history
- summary
- last progress

## 7.4 Field access
I field access sono stringhe, non oggetti:
- esempio: `"Edit"`, `"Read"`

Quindi nei controlli bisogna fare confronti del tipo:

`csharp
string.Equals(access, "Edit", StringComparison.OrdinalIgnoreCase)

e non usare proprietà inesistenti tipo .CanWrite.

Questo è stato un punto importante emerso durante step 13.

## 8. Regole implementative da NON rompere
## 8.1 Self e Team restano separati

Non riusare pagine self per target manageriale.
Non riusare MyWorkflowContextResolver per il manager flow.

## 8.2 Tenant query string sempre preservato

Tutte le navigazioni:

/my
/team
detail goal/assessment self/team

devono preservare __tenant.

## 8.3 Target manageriale preservato

Nel flow manager:

usare targetEmployeeId in query string quando si torna dai detail alla dashboard team
## 8.4 Service user-facing come entrypoint UI

La UI Blazor deve passare da:

My*
Team*

Non deve chiamare direttamente service tecnici salvo casi già consolidati come IGoalProgressAppService, che è una decisione già voluta e consolidata nel progetto.

## 8.5 Logica vera lato server

Anche se la UI abilita/disabilita pulsanti:

il server deve sempre fare enforcement reale su permessi, fase, stato, field access
## 9. Problemi già incontrati e già risolti
tenant perso nella navigazione interna -> risolto preservando ?__tenant=...
conflitto ValidationSummary Blazor / Blazorise
niente HttpClient raw per GoalProgress
niente proxy remoto separato per CompetencyAssessment direttamente in UI
ICompetencyAssessmentAppService non disponibile in UI manager/self -> uso facade/layer giusto
save della prima riga competenza assessment self -> risolto
submit assessment self -> risolto
Advance phase self -> risolto
manager scope vuoto -> era dovuto a dati EmployeeManagers.EndDate nel passato
local run sul PC -> risolto aggiornando connection string locale
global.json locale -> risolto con SDK locale compatibile
Git non installato sul PC -> risolto
push su GitHub -> risolto
Team.razor con target preservato -> funzionante con OnParametersSetAsync + query param
## 10. Dati / DB: verità operative importanti
## 10.1 Scope manageriale

Fonte reale:

Employees
EmployeeManagers
## 10.2 OrgUnit

Esistono:

OrgUnit
Employees.OrgUnitId

Ma oggi:

non esiste una relazione manager -> org unit usabile come scope manageriale

Quindi:

non usare ancora OrgUnit per i permessi manageriali
se in futuro si estende, farlo dietro un resolver di scope dedicato
## 10.3 Login employee

Perché un employee entri in /my, serve:

utente ABP esistente
Employees.UserId valorizzato con l’Id dell’utente ABP

Senza questo, il resolver self fallisce.

## 10.4 Target di test usati finora

Esempi reali emersi durante sviluppo:

Mauro Lisai come manager
Federico Bertelli
Cicillo Cacace
matricole come M001, M002, M004

Attenzione:

alcuni problemi osservati erano di dati, non di codice
in particolare EmployeeManagers.EndDate nel passato esclude correttamente un subordinato dallo scope manageriale
## 11. Convenzioni di lavoro consigliate
## 11.1 Ogni step deve essere piccolo

Continuare con step piccoli e compilabili è stata una strategia vincente.

Formato desiderato:

- 1 obiettivo
- pochi file
- modifica mirata
- compilazione
- test
- poi step successivo

## 11.2 Prima compilare, poi rifinire

Priorità:

build ok
run ok
test minimo ok
polish
## 11.3 Usare file reali, non ipotesi

Quando si lavora su nuovi step:

leggere sempre il file reale prima
evitare ipotesi sui nomi metodi/classi se non necessario
## 11.4 Aggiornare questo file

Dopo ogni step importante:

aggiornare PROJECT_HANDOFF.md
fare commit Git
## 12. Stato Git / ambiente locale

Situazione attuale:

progetto copiato sul PC
compilazione locale funzionante
run locale funzionante
connection string locale corretta
Git installato
repo inizializzato
push GitHub completato

Consiglio operativo:

lavorare solo sulla copia locale del PC
non sviluppare da share di rete del server
usare il server solo come riferimento/storico o per esigenze specifiche

## 13. Stato corrente esatto da cui ripartire
Step completati
self flow base
self goal detail + add progress
self assessment detail + save item + submit
self advance phase
manager dashboard /team
manager target preservation
manager read-only goals
manager read-only assessments
manager goal detail
manager assessment detail
manager add progress goal
manager submit assessment
manager edit assessment item (step 13 compilato)
Ultimo risultato confermato
step 13 compilato
modifiche ai goals di Cicillo Cacace da parte del manager Mauro Lisai testate con successo
team read-only ok
self flow ok
local environment ok
GitHub push ok
## 14. Prossimi passi consigliati (ordine suggerito)
Alta priorità
rifinire UX assessment item edit manager
dirty tracking
bulk save / save all
messaggi migliori
consolidare submit/edit assessment manager
iniziare HR flow
Media priorità
HR dashboard / target flow
admin/config minima seria
collegare meglio identity/user linking
migliorare navigation e UX polished
Bassa priorità ma importante per “finale completo”
import UI per goals/progress
smoke test completi
hardening bugfix
polishing produzione
readiness deploy finale
## 15. Nota finale importantissima

Questo progetto non è più in fase esplorativa.

Le decisioni architetturali più importanti sono già emerse e vanno rispettate:

My* = self
Team* = manager
workflow = fonte di verità
manager scope = EmployeeManagers
tenant sempre preservato
service user-facing come entrypoint UI
enforcement vero lato server

Le prossime modifiche devono essere evolutive, non rivoluzionarie.



- STEP 14.1 completato: bulk update lato server
- step 14.2 completato: dirty tracking lato UI
- step 14.3 completato: polish UX/error handling
STEP 15 completato: polish UX del detail assessment manager
STEP 16 completato: primo HR flow base
STEP 17 completato: HR target flow read-only con cambio ciclo e selezione dipendente. Descrizione: la dashboard HR permette di cambiare ciclo, selezionare un dipendente del ciclo e visualizzare in sola lettura goals e assessments del target, mantenendo il tenant in query string e senza toccare i flow My* e Team*.
STEP 18 completato: HR detail dedicati per goals e assessments target. Descrizione: Aggiunte pagine HR dedicate per il dettaglio read-only di goals e assessments del target, collegate dalla dashboard HR e con preservazione di cycleId, targetEmployeeId e tenant nella navigazione.
STEP 19 completato: Admin config minima workflow e permessi. Descrizione: Aggiunta una prima area admin per configurare template/fasi, role permissions e field policies del workflow, con create/update/delete, validazioni minime e preservazione del tenant in navigazione.
STEP 20 completato: identity linking admin tra utenti ABP e dipendenti. Descrizione: Aggiunta una pagina admin per cercare utenti ABP, collegarli o scollegarli dai dipendenti del tenant e tentare il link automatico per email, eliminando la dipendenza dagli script SQL per il mapping identity-employee.
STEP 21 completato: admin employee management base. DEscrizione: Aggiunta una pagina admin per creare e modificare gli employee del tenant, con lookup OrgUnit/JobRole, stato di collegamento utente e integrazione operativa con la pagina di identity linking.
STEP 22 completato: import employee CSV base. Descrizione: Aggiunto import CSV degli employee nella pagina Admin Employees, con validazione completa lato server, upsert opzionale per matricola e blocco del salvataggio se una qualsiasi riga contiene errori.
STEP 23 completato: hardening admin tenant e ruolo. DEscrizione: Rafforzati i controlli server-side sulle superfici admin, richiedendo tenant e ruolo admin per workflow config, employee admin e identity linking, con tenant esplicito sulle nuove creazioni employee e limiti sui lookup.
STEP 24 completato: production readiness checklist e smoke test runbook. Descrizione: Aggiunta una checklist operativa di readiness produzione con smoke test Self, Team, HR e Admin, controlli dati minimi, criteri pre-deploy e rischi residui noti.
STEP 25 completato: admin cycle management base. Descrizione: Aggiunta una pagina admin per creare e modificare cicli performance del tenant, con lookup template, fase corrente, status, date ciclo e enforcement server-side admin/tenant.
STEP 26 completato: cycle participant management base. Descrizione: Aggiunta una pagina admin per gestire i partecipanti di un ciclo, con selezione ciclo, add/edit/remove participant, fase corrente, status partecipazione, preservazione tenant in query string ed enforcement server-side admin/tenant.
STEP 27 completato: manager relation admin base. Descrizione: Aggiunta una pagina admin per gestire `EmployeeManagers` da UI, con add/edit/end/remove relazioni, selezione employee e manager, relation type, primary, start/end date, validazioni su duplicati/self-manager/date e enforcement server-side admin/tenant.
STEP 28 completato: import esteso setup performance. Descrizione: Aggiunta una pagina admin `/admin/performance-import` per importare in modo atomico `CycleParticipants` e `EmployeeManagers`, con due CSV separati, validazione completa prima del commit, update opzionale delle righe esistenti, report riga-per-riga e blocco totale del salvataggio se una qualsiasi riga contiene errori.
STEP 29 completato: final hardening e deploy rehearsal base. Descrizione: Aggiunta una pagina admin `/admin/readiness` read-only per controlli pre-deploy su tenant, ruolo admin, workflow template/fasi/policy, cicli attivi, participants, employee link, manager relations e assessment, con badge Ok/Warning/Error e aggiornamento della checklist produzione.
STEP 30 completato: OrgUnit admin management base. Descrizione: Aggiunta una pagina admin `/admin/org-units` per creare, modificare e rimuovere OrgUnit del tenant, con parent OrgUnit, vista gerarchica, conteggio figli/employee, validazione anti-loop lato server e blocco delete se la OrgUnit e referenziata da employee o OrgUnit figlie.
STEP 31 completato: JobRole admin management base. Descrizione: Aggiunta una pagina admin `/admin/job-roles` per creare, modificare e rimuovere JobRole del tenant, con conteggio employee collegati, enforcement admin/tenant lato server, validazione duplicati e blocco delete se il ruolo e referenziato da employee.
STEP 32 completato: Org chart e struttura organizzativa read-only. Descrizione: Aggiunta una pagina admin `/admin/org-chart` read-only per visualizzare OrgUnits in forma gerarchica, employee diretti per OrgUnit, JobRole, manager primario attivo derivato da `EmployeeManagers` ed employee senza OrgUnit, senza modificare lo scope manageriale.
STEP 33 completato: manager relations admin hardening. Descrizione: Rafforzata la pagina `/admin/manager-relations` con ricerca su employee e manager, filtro sulle relazioni, separazione tra relazioni attive, future e chiuse, badge di stato e messaggi errore piu leggibili, mantenendo lo scope manageriale basato su `EmployeeManagers`.
STEP 34 completato: cycle management hardening. Descrizione: Rafforzata la pagina `/admin/cycles` con contatori setup per participant, goal e assessment, azioni controllate Attiva/Chiudi e protezioni server-side su attivazione, chiusura, cambio template e transizioni di stato del ciclo.
STEP 35 completato: cycle participants admin hardening. Descrizione: Rafforzata la pagina `/admin/cycle-participants` con ricerca e filtri sui participant, bulk add atomico degli employee attivi mancanti, reset controllato della fase participant e blocco server-side delle modifiche quando il ciclo e chiuso.
STEP 36 completato: goal catalog admin. Descrizione: Aggiunta la pagina `/admin/goal-catalog` per gestire il catalogo goal del tenant, con create/edit, category, description, default weight, flag library item, ricerca/filtri, conteggio assegnazioni e blocco delete se il goal e gia assegnato.
STEP 37 completato: goal assignment admin. Descrizione: Aggiunta la pagina `/admin/goal-assignments` per assegnare goal del catalogo ai participant del ciclo selezionato, con weight, target value, date, status, ricerca/filtri, preservazione tenant, validazione participant/ciclo/goal, blocco duplicati, somma pesi massima 100 e blocco modifiche sui cicli chiusi.
STEP 38 completato: competency catalog admin. Descrizione: Aggiunta la pagina `/admin/competency-catalog` per gestire il catalogo competenze del tenant, con create/edit, code, name, description, flag active, ricerca/filtri, conteggio referenze da competency model item e assessment item, enforcement tenant/admin e blocco delete se la competenza e referenziata.
STEP 39 completato: competency model admin. Descrizione: Aggiunta la pagina `/admin/competency-models` per gestire competency model e relative righe item, con scale type, min/max score, competency item, required, weight, validazione somma weight massima 100, blocco duplicati, preservazione tenant e blocco modifiche strutturali quando il model e gia usato da assessment.
STEP 40 completato: assessment setup admin. Descrizione: Aggiunta la pagina `/admin/assessment-setup` per generare assessment per ciclo/participant/evaluator/type/model, con creazione item da competency model, blocco sui cicli chiusi, blocco rigenerazione su assessment Submitted/Closed e rigenerazione sicura dei Draft tramite aggiunta dei soli item mancanti senza sovrascrivere score/commenti.
STEP 41 completato: HR reporting base. Descrizione: Aggiunta la pagina `/hr/reports` read-only per report operativi HR per ciclo, con conteggi participant per fase/status, assessment per status/type, assessment con required mancanti, participant attivi senza manager attivo e participant attivi senza user link, preservando tenant e senza modificare i flow My* e Team*.
STEP 42 completato: Team reporting manager base. Descrizione: Aggiunta la pagina `/team/reports` read-only per il manager loggato, con scope derivato solo da `EmployeeManagers`, selezione ciclo, overview subordinati, participant per fase, goal per status, goal senza progress o in ritardo, assessment Manager mancanti e assessment con required mancanti, preservando il tenant e senza modificare il flow operativo Team.

Nota roadmap aggiornata: lo STEP 29 non rappresenta la fine completa del progetto, ma la chiusura di una prima tranche deployabile. La roadmap estesa in `docs/NEXT_STEPS.md` arriva ora fino a STEP 57 e include OrgUnits, JobRoles, org chart, manager relations, cycle setup completo, cataloghi goal/competency, reporting, export, SSO Active Directory/Entra ID, audit log, notifiche, security review, deploy staging, deploy produzione e post go-live support.

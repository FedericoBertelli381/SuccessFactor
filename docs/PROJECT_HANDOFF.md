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

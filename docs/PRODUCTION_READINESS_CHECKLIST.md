# Production Readiness Checklist - SuccessFactor

Questa checklist serve per validare la soluzione prima del deploy definitivo.

## 1. Build e avvio

- Build completa della solution da Visual Studio.
- Nessun errore Razor/Blazor attivo.
- Avvio applicazione Blazor.
- Connessione al database corretta.
- Tenant raggiungibile con `?__tenant=...`.
- Login funzionante per almeno questi profili: admin, employee self, manager, HR.

## 2. Smoke test Self

- Aprire `/my?__tenant=...`.
- Verificare caricamento dashboard.
- Verificare lista goals.
- Aprire un goal detail self.
- Aggiungere un progress se la fase lo consente.
- Verificare lista assessments.
- Aprire assessment detail self.
- Salvare una riga competenza se in `Draft`.
- Fare submit assessment solo quando required score sono valorizzati.
- Verificare che post-submit la UI diventi read-only.

## 3. Smoke test Team

- Login come manager.
- Aprire `/team?__tenant=...`.
- Verificare lista subordinati da `EmployeeManagers`.
- Cambiare target e verificare query string `targetEmployeeId`.
- Aprire goal detail target.
- Aggiungere progress se consentito dal workflow.
- Aprire assessment detail target.
- Modificare piu righe competenza.
- Verificare dirty tracking.
- Usare `Save all`.
- Verificare che non sia possibile fare submit con dirty item pendenti.
- Fare submit quando required score sono valorizzati.
- Verificare read-only dopo submit.
- Verificare `Advance phase` solo quando consentito.

## 4. Smoke test HR

- Login con utente HR.
- Aprire `/hr?__tenant=...`.
- Cambiare ciclo.
- Cambiare dipendente target.
- Verificare goals target read-only.
- Verificare assessments target read-only.
- Aprire HR goal detail.
- Tornare a HR e verificare preservazione `cycleId`, `targetEmployeeId` e `__tenant`.
- Aprire HR assessment detail.
- Verificare che non esistano azioni di edit/submit da HR detail.

## 5. Smoke test Admin Workflow

- Login admin.
- Aprire `/admin/workflow?__tenant=...`.
- Selezionare template e fase.
- Creare, modificare ed eliminare una role permission di test.
- Creare, modificare ed eliminare una field policy di test con access `"Read"` o `"Edit"`.
- Verificare che un utente non admin non possa usare il facade admin.

## 6. Smoke test Admin Employees

- Aprire `/admin/employees?__tenant=...`.
- Creare un employee con matricola nuova.
- Modificare email o `IsActive`.
- Verificare lookup `OrgUnit` e `JobRole`.
- Importare un CSV valido.
- Importare un CSV con un errore e verificare che nessuna riga venga salvata.
- Importare un CSV con `OrgUnit` e `JobRole` vuoti.
- Verificare che il tenant venga preservato nel passaggio ad Admin Users.

Formato CSV employee:

```csv
Matricola;FullName;Email;OrgUnit;JobRole;IsActive
M011;Catia Esposito;c.esposito@lesoluzioni.net;;;true
M012;Mario Rossi;m.rossi@lesoluzioni.net;-;-;true
```

## 7. Smoke test Admin Users

- Aprire `/admin/identity-link?__tenant=...`.
- Verificare employee non collegati.
- Cercare utenti ABP.
- Collegare employee e utente manualmente.
- Scollegare il mapping.
- Provare `Link by email` su employee con email corrispondente a un utente.
- Verificare che un utente non admin non possa accedere al linking.

## 8. Controlli dati minimi

- Ogni employee che deve usare `/my` ha `Employees.UserId` valorizzato.
- Ogni manager ha almeno una relazione attiva in `EmployeeManagers`.
- Le relazioni `EmployeeManagers` hanno date valide.
- I partecipanti del ciclo hanno `CurrentPhaseId` valorizzato dove necessario.
- Le policy workflow usano accessi come stringhe: `"Hidden"`, `"Read"`, `"Edit"`.
- I tenant dei record creati da admin/import sono coerenti con il tenant corrente.

## 9. Criteri minimi per deploy

- Build Visual Studio senza errori.
- Smoke test Self completato.
- Smoke test Team completato.
- Smoke test HR completato.
- Smoke test Admin completato.
- Nessun errore tenant nelle navigazioni principali.
- Nessuna dipendenza obbligatoria da script SQL per employee e user linking base.
- Backup database disponibile prima del deploy.
- Connection string e tenant configurati per ambiente target.

## 10. Rischi residui noti

- La gestione completa di cycle participants e manager relations da UI e ancora da completare.
- L'import CSV copre employee base, non ancora goal assignments o cycle participants.
- L'area admin e funzionale ma ancora minimale.
- La readiness finale richiede una prova deploy su ambiente target.

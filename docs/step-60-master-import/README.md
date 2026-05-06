# STEP 60 - Master Import HR

Questa cartella contiene un esempio completo da usare come base per l'onboarding di un nuovo tenant.

## Ordine consigliato

1. Creare o attivare il tenant.
2. Entrare nell'app con l'URL del tenant:

```text
/admin/performance-import?__tenant=<NOME_TENANT>
```

3. Aprire [PerformanceImportAdmin.razor](C:/Progetti/SuccessFactor/src/SuccessFactor.Blazor/Pages/Admin/PerformanceImportAdmin.razor).
4. Copiare ogni file di esempio nella sezione corrispondente.
5. Lasciare attivo `Aggiorna righe esistenti se trovate` se il tenant viene riallineato, disattivarlo se si vuole intercettare ogni duplicato.
6. Avviare l'import.
7. Se l'import e' verde, passare ai controlli finali:
   - `/admin/readiness?__tenant=<tenant>`
   - `/admin/employees?__tenant=<tenant>`
   - `/admin/identity-link?__tenant=<tenant>`
   - login di un utente reale

## Come creare un nuovo tenant di un'altra azienda

### Situazione attuale della piattaforma

Nel codice applicativo e' presente il modulo ABP Tenant Management, ma nella UI amministrativa consegnata oggi **non c'e' una pagina tenant dedicata nel menu**. Questo significa che il tenant non viene ancora creato da un admin funzionale dentro la console SuccessFactor.

In pratica, oggi il flusso reale e' questo:

1. **Passo tecnico iniziale**
   - il tenant va creato da un utente tecnico/host usando ABP Tenant Management se esposto nell'ambiente, oppure con attivita tecnica lato database/applicazione.

2. **Passo amministrativo funzionale**
   - una volta che il tenant esiste, l'admin del tenant puo fare il resto dall'app:
     - OrgUnits
     - JobRoles
     - Employees
     - Identity Links
     - User Roles
     - Participants
     - Manager Relations
     - Goal Assignments
     - Competencies
     - Competency Models

### Procedura consigliata

#### Scenario A - hai un accesso host con Tenant Management esposto

Se nell'ambiente e' disponibile la gestione tenant di ABP:

1. entra come host admin;
2. crea il tenant con il nome dell'azienda, per esempio `ACME SPA`;
3. annota il nome esatto del tenant;
4. crea o collega un utente amministratore del tenant;
5. assegna il ruolo applicativo `admin`;
6. apri l'app con:

```text
?__tenant=ACME SPA
```

7. usa il Master Import HR.

#### Scenario B - la UI tenant non e' esposta

Questo e' il caso piu probabile nello staging attuale.

In questo scenario:

1. chiedi la creazione tecnica del tenant;
2. fatti comunicare il **nome tenant esatto**;
3. verifica che esista almeno un utente admin del tenant;
4. accedi con:

```text
?__tenant=<nome tenant esatto>
```

5. carica i dati con i file di questa cartella.

### Dati minimi che servono per aprire davvero un tenant

Prima del Master Import, devono esistere almeno:

- tenant
- utente amministratore del tenant
- ruolo `admin` assegnato a quell'utente

Senza questi tre elementi, il tenant esiste formalmente ma non e' governabile dall'interfaccia.

## Note pratiche

- I riferimenti dei file sono pensati per essere incollati nelle textarea dell'import.
- Gli esempi usano separatore `;`.
- I ruoli applicativi ammessi sono:
  - `admin`
  - `HR`
  - `Responsabile`
  - `Dipendente`
- I file sono volutamente piccoli e leggibili: vanno clonati e ampliati in base all'azienda reale.

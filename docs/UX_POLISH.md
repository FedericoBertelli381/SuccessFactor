# UX polish globale

Lo step 50 introduce una base UI riusabile per rendere piu coerenti loading, errori, empty state e intestazioni pagina.

## Componenti aggiunti

| Componente | Uso |
| --- | --- |
| `PageHeader` | titolo, sottotitolo e azioni pagina coerenti |
| `LoadingState` | stato di caricamento con spinner e testo |
| `StatusAlert` | messaggi di errore/successo standardizzati |
| `EmptyState` | stato vuoto leggibile e riusabile |

## Pagine aggiornate

- `Admin Audit Log`
- `Admin Notifications`

Queste pagine fanno da riferimento per il polish progressivo delle altre viste Admin, HR, Team e My.

## Regole operative

- usare `PageHeader` per nuove pagine o quando si rifinisce una pagina esistente;
- usare `LoadingState` quando il primo caricamento non ha ancora dati;
- usare `StatusAlert` per errori e conferme utente;
- usare `EmptyState` quando una tabella/lista non ha risultati;
- mantenere azioni principali in alto a destra e filtri vicino ai dati;
- evitare testo tecnico non comprensibile quando esiste un messaggio business piu chiaro.

## Test UI

1. Aprire una pagina aggiornata con dati disponibili.
2. Provare una ricerca senza risultati e verificare empty state.
3. Forzare un errore applicativo e verificare alert.
4. Verificare layout su desktop e finestra stretta.
5. Verificare che i pulsanti restino disabilitati durante loading/send.

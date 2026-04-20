# Notification/email readiness

Lo step 49 prepara la piattaforma all'invio di comunicazioni operative via email.

## Pagina Admin

La pagina e disponibile dal menu Admin:

```text
Admin Notifications
```

Route:

```text
/admin/notifications
```

Funzioni disponibili:

- lettura stato configurazione email;
- elenco impostazioni mancanti;
- anteprima template;
- invio mail di test quando la configurazione e completa.

## Configurazione

Configurare `appsettings.json` o variabili ambiente equivalenti:

```json
{
  "SuccessFactor": {
    "Notifications": {
      "Email": {
        "Enabled": true,
        "Provider": "Smtp",
        "FromAddress": "successfactor@azienda.it",
        "FromName": "SuccessFactor",
        "Smtp": {
          "Host": "smtp.azienda.it",
          "Port": 587,
          "UserName": "successfactor@azienda.it",
          "Password": "<secret>",
          "EnableSsl": true
        }
      }
    }
  }
}
```

In ambienti reali non salvare password in repository. Usare secret manager, variabili ambiente o secret store del provider.

## Template pronti

| Codice | Uso |
| --- | --- |
| `CycleOpened` | notifica apertura ciclo |
| `MissingAssessmentReminder` | reminder assessment mancanti |
| `AssessmentSubmitted` | conferma submit assessment |
| `ManagerReminder` | reminder manager |

## Test operativo

1. Configurare SMTP.
2. Impostare `SuccessFactor:Notifications:Email:Enabled` a `true`.
3. Aprire `Admin Notifications`.
4. Verificare stato verde.
5. Inserire un destinatario reale.
6. Inviare una mail di test.
7. Verificare ricezione, mittente, oggetto e contenuto.

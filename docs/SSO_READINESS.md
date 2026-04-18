# SSO readiness - Active Directory / Entra ID

## Stato

Lo SSO e predisposto tramite provider esterno OpenID Connect configurabile da `Sso:OpenIdConnect`.

Default:
- `Enabled = false`
- login locale ABP/OpenIddict invariato
- fallback admin locale sempre disponibile

Provider target consigliato:
- Microsoft Entra ID con OpenID Connect.

Provider compatibili:
- ADFS se espone OpenID Connect.
- Identity provider aziendale compatibile OpenID Connect.

SAML non e collegato direttamente in questa base applicativa. Se il provider aziendale espone solo SAML, usare un bridge verso OpenID Connect o aggiungere un handler SAML dedicato in uno step separato.

## Configurazione applicazione

File: `src/SuccessFactor.Blazor/appsettings.json`

```json
"Sso": {
  "OpenIdConnect": {
    "Enabled": true,
    "Scheme": "CorporateOidc",
    "DisplayName": "Corporate SSO",
    "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
    "ClientId": "{application-client-id}",
    "ClientSecret": "{client-secret}",
    "ResponseType": "code",
    "CallbackPath": "/signin-corporate-oidc",
    "SignedOutCallbackPath": "/signout-corporate-oidc",
    "RequireHttpsMetadata": true,
    "SaveTokens": true,
    "GetClaimsFromUserInfoEndpoint": true,
    "Scopes": [ "openid", "profile", "email" ],
    "Claims": {
      "Name": "name",
      "UserName": "preferred_username",
      "Email": "email",
      "Role": "roles"
    }
  }
}
```

In produzione impostare `ClientSecret` tramite secret store, variabile ambiente o configurazione protetta, non nel file versionato.

## Entra ID

Registrazione app:
- piattaforma: Web.
- redirect URI: `{App:SelfUrl}/signin-corporate-oidc`.
- front-channel logout URL, se richiesto: `{App:SelfUrl}/signout-corporate-oidc`.
- grant: authorization code.
- token: ID token non necessario per il code flow puro, ma puo essere lasciato abilitato se la policy aziendale lo richiede.
- claim email: assicurarsi che `email` o `preferred_username` sia valorizzato.
- claim ruoli: esporre `roles` se si vogliono mappare ruoli applicativi da app roles.

Authority:
- single tenant: `https://login.microsoftonline.com/{tenant-id}/v2.0`
- multi tenant: `https://login.microsoftonline.com/organizations/v2.0`

## ADFS / AD on-prem

AD on-prem non parla direttamente con questa configurazione. Serve ADFS o un altro identity provider che esponga OpenID Connect.

Esempio authority ADFS:
- `https://adfs.contoso.local/adfs`

Da verificare con il team infrastruttura:
- endpoint discovery `/.well-known/openid-configuration`.
- callback consentita `/signin-corporate-oidc`.
- claim name, email, username e roles/groups.
- certificati TLS validi e raggiungibili dall'applicazione.

## Mapping utenti

Il linking applicativo esistente resta basato sugli utenti ABP collegati agli employee.

Regola consigliata:
- `preferred_username` o `email` deve corrispondere allo username/email ABP.
- l'admin locale deve rimanere disponibile per emergenze e primo setup.
- i ruoli applicativi canonici sono `admin`, `HR`, `Responsabile`, `Dipendente`.
- i claim esterni `role`, `roles`, `groups` vengono normalizzati verso i ruoli canonici quando combaciano con `Sso:RoleMappings`.
- se Entra ID espone app roles, usare valori come `SuccessFactor.Admin`, `SuccessFactor.HR`, `SuccessFactor.Manager`, `SuccessFactor.Employee`.

Esempio:

```json
"Sso": {
  "RoleMappings": {
    "admin": [ "SuccessFactor.Admin", "SF_Admin" ],
    "HR": [ "SuccessFactor.HR", "SF_HR" ],
    "Responsabile": [ "SuccessFactor.Manager", "SF_Manager" ],
    "Dipendente": [ "SuccessFactor.Employee", "SF_Employee" ]
  }
}
```

## Test

Smoke test minimi:
- con `Enabled = false`, il login locale continua a funzionare.
- con `Enabled = true`, nella pagina login compare il provider `DisplayName`.
- click sul provider redirige all'identity provider.
- callback su `/signin-corporate-oidc` completa il login.
- utente loggato accede a `/my` se collegato a un employee.
- utente senza ruoli applicativi non accede alle pagine admin/HR/manager.
- claim esterno `SuccessFactor.HR` abilita le pagine HR ma non le pagine Admin.
- claim esterno `SuccessFactor.Manager` abilita le pagine Team ma non le pagine Admin/HR.
- logout applicativo chiude la sessione locale.
- fallback admin locale funziona anche se il provider esterno non e disponibile.

## Rollback

Per disattivare SSO:
- impostare `Sso:OpenIdConnect:Enabled = false`.
- riavviare l'applicazione.
- usare login locale ABP.

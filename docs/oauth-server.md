# Local OAuth server

`Bookstore.Auth` uses OpenIddict 7.7.1 and ASP.NET Core Identity on .NET 10.
It issues tokens that the API validates, with separate scopes for CRUD and search.
See the [API security/Swagger guide](api-security-swagger.md) for the demonstration.
The recommended [Docker quick start](../README.md#docker-quick-start-recommended)
runs without a host SDK, Visual Studio, User Secrets, or `dotnet dev-certs`.
Optional local development runs the applications against the existing SQL Server
container instead.

## Clients, user, and endpoints

| Application | Grant | Scope | Secret |
| --- | --- | --- | --- |
| `bookstore-management` | Client credentials | `books.manage` | Required; stored in ignored Docker `auth.json` for the Docker demo or Auth User Secrets locally, then hashed by OpenIddict in SQL. |
| `bookstore-browser` | Implicit (`response_type=token`) | `books.search` | None; this is a public client. |

These are two applications. `demo@bookstore.local` is the single development
human user who signs in for the browser flow. Management requests do not log in
as that user. There is no registration, password reset, or author-management UI.

| URL | Purpose |
| --- | --- |
| `https://localhost:7200/.well-known/openid-configuration` | Issuer metadata and endpoint discovery. |
| `https://localhost:7200/connect/token` | POST a client-credentials request. |
| `https://localhost:7200/connect/authorize` | GET an implicit authorization request; challenges unauthenticated users. |
| `https://localhost:7200/account/login` | Identity login form. |
| `https://localhost:7200/` | Account status and a POST form for logout. |
| `https://localhost:7200/demo` | Development-only browser flow check. |

The issuer is exactly `https://localhost:7200/`. Use `localhost` consistently for
the Auth browser URLs; `127.0.0.1` is only the SQL connection address. Auth's sole
launch profile is `https`; it does not offer a plaintext HTTP login mode. Even if
an HTTP listener is explicitly configured, the application rejects HTTP requests.

## Optional local / Visual Studio development

This section is not needed for the recommended Docker quick start. First complete
the [optional SQL Server/API configuration](../README.md#optional-local-database-setup).
Then, from the repository root:

```powershell
docker compose up -d --wait sqlserver
dotnet dev-certs https --trust
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-AuthDevelopment.ps1
dotnet run --project src/Bookstore.Auth --launch-profile https
```

Trusting the development HTTPS certificate can show a Windows confirmation.
The setup helper copies the existing API SQL connection, changes only its database
to `BookstoreAuth`, and generates a random management secret and demo password.
It preserves existing Auth settings and prints no passwords. It changes neither
the API connection nor `.env`. It is a Windows convenience; it is not required by
the runtime.

In Visual Studio, select **Bookstore.Auth > Manage User Secrets** to view the
generated settings. To configure them manually (also supported on other systems),
use the following shape, replacing placeholders and preserving other entries:

```json
{
  "ConnectionStrings:Authentication": "Server=tcp:127.0.0.1,14333;Database=BookstoreAuth;User Id=sa;Password=<your-local-SQL-password>;Encrypt=True;TrustServerCertificate=True",
  "DevelopmentDemo:ManagementClientSecret": "<strong-random-client-secret>",
  "DevelopmentDemo:UserPassword": "<strong-demo-password>"
}
```

The user-secrets ID is `bookstore-auth-development`, separate from the API's.
The demo password must have at least 12 characters, including upper/lowercase
letters, a digit, and a symbol. User secrets are local configuration outside Git,
not an encrypted production secret store. The `sa` SQL login and
`TrustServerCertificate=True` remain local development conveniences.

For Visual Studio 2026, set **Bookstore.Auth** as the startup project and select
the **https** profile. Its startup applies `InitialAuthentication` and seeds any
missing demo records in Development. The database shares the existing SQL Server
container and named volume with `Bookstore`; it has separate Identity and
OpenIddict tables. No Docker volume or existing book data is reset.

## Try both flows

For the Docker client-credentials flow, use the [Docker management-token command]
(api-security-swagger.md#management-token-and-crud). It reads the local ignored
`.local/docker/auth.json`, not User Secrets, and keeps the access token in memory.

For optional local/Visual Studio hosting, keep the response in memory instead of
displaying the access token or placing a secret in command history:

```powershell
$secretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\bookstore-auth-development\secrets.json'
$authSettings = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
$token = Invoke-RestMethod -Method Post -Uri 'https://localhost:7200/connect/token' -Body @{
    grant_type = 'client_credentials'
    client_id = 'bookstore-management'
    client_secret = $authSettings.'DevelopmentDemo:ManagementClientSecret'
    scope = 'books.manage'
}
$token | Select-Object token_type, expires_in, scope
```

For implicit, visit `https://localhost:7200/demo`, click **Continue to sign in**,
and sign in as `demo@bookstore.local`. In Docker, use the deliberately copied
password from `.local/docker/auth.json`; in optional local hosting, use the password
from Auth User Secrets.
The server returns an access token in the callback URL fragment. The page checks
the one-time `state`, removes the fragment immediately, reports the outcome, and
discards the token. It does not call the book API or persist tokens in browser
storage. Only the temporary state is held in session storage during the redirect.
Use **Back to your account > Sign out** to clear the Identity session.

The initial browser registration allows exactly these HTTPS callbacks:

- `https://localhost:7200/demo/callback` for the standalone flow check.
- `https://localhost:7100/swagger/oauth2-redirect.html` for Swagger search authorization.

Unregistered callback addresses are rejected. The browser client can request only
`books.search`; management can request only `books.manage`. Missing scopes and
requests combining either scope with unrelated scopes are rejected too. No
password, refresh-token, or authorization-code grant is enabled. OpenIddict
enforces endpoint, grant, scope, and response-type permissions; see its
[application permission documentation](https://documentation.openiddict.com/configuration/application-permissions.html).

The implicit flow is implemented because the assignment requires it. It is a
legacy choice: a production browser client should use Authorization Code with
PKCE. That recommendation does not replace the required assignment flow. See
[OpenIddict's flow guidance](https://documentation.openiddict.com/guides/choosing-the-right-flow.html).

## Decisions and repeat startup

Access tokens are signed JWTs with a 15-minute lifetime, issuer
`https://localhost:7200/`, audience `bookstore-api`, a subject, and the granted
scope. A management subject is its client ID; a browser subject is the Identity
user's ID. A fresh token identity prevents copying password hashes, security
stamps, or other cookie claims into a token. Access-token encryption is explicitly
disabled so the separate API can validate signatures using public discovery keys.
HTTPS remains required. See [token formats](https://documentation.openiddict.com/configuration/token-formats.html)
and [claim destinations](https://documentation.openiddict.com/configuration/claim-destinations.html).

Identity hashes the demo password and locks an account for five minutes after
five failed login attempts. The session cookie is Secure, HttpOnly, SameSite=Lax,
and limited to 30 minutes without sliding renewal. Login and logout forms require
antiforgery tokens, and login return URLs must be local. Logout clears the browser
session; already-issued JWTs retain their expiry. The first-party browser client
uses implicit consent, so no separate consent screen is needed for this demo.
Identity's cookie handling and form protection follow the
[Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)
and [antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0) guidance.

The seeder runs only at Development startup after migrations. Inside one SQL
transaction, an application lock serializes seeders; each missing application or
the demo username is created once. Existing records are never updated. Changing
the configured password, client secret, or redirect list therefore does not reset
an existing user's password or overwrite a client. Intentional credential or
registration changes require controlled provisioning using the Identity and
OpenIddict managers, rather than changing seed settings or deleting a database.
The migration contains schema only. `dotnet ef database update` applies that
schema; demo seeding happens on application startup, not inside the migration.

Optional local development signing and encryption certificates persist in the
current user's certificate store and are separate from the HTTPS certificate. Docker
instead mounts generated ignored PFX files. OpenIddict's protocol information/debug
logs are suppressed because they can contain tokens or request values. Unexpected
application errors use generic ProblemDetails and log only exception type and trace
ID. Protocol rejections keep OpenIddict's OAuth error format (including plain text
when an authorization request has no safe callback), rather than replacing it with
application errors.

## Controlled Production setup

Production startup performs no migrations or demo seeding. Before deployment:

1. Provision `BookstoreAuth`, using a deployment login for schema changes and a
   separate restricted runtime login.
2. Supply `ConnectionStrings__Authentication` securely and review/apply the schema
   using the commands below with the Production environment.
3. Configure `Auth__Issuer` with the deployed HTTPS issuer and provision the two
   clients through OpenIddict's application manager with the grants, scopes, and
   exact deployed callback URIs above. Set `Auth:BrowserRedirectUris` to those
   HTTPS callbacks as well, so the login form's security policy permits their
   origins during browser redirects. This configuration does not overwrite
   database registrations. Provision real Identity users through
   `UserManager`; do not insert plaintext credentials into database tables.
4. Supply separate RSA signing and encryption certificates with private keys,
   using `Certificates__Signing__Path`, `Certificates__Signing__Password`,
   `Certificates__Encryption__Path`, and `Certificates__Encryption__Password`.
   Keep the files and passwords out of Git. Configure host HTTPS separately and
   plan key rotation and persistent ASP.NET Core Data Protection keys.
5. Set `DataProtection__KeysPath` to a persistent directory writable by the Auth
   process. The existing configuration sets application name `Bookstore.Auth`
   and encrypts the key ring with the configured encryption certificate. Back up
   that certificate and its password together with the key ring, and retain old
   decryption material during a planned rotation. See the
   [Production checklist](delivery-guide.md#production-checklist) for deployment
   boundaries and reverse-proxy requirements.

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent --project src/Bookstore.Auth --output .artifacts/auth-migrations.sql -- --environment Production
# Apply only after review and provisioning; the connection comes from secure configuration.
dotnet ef database update --project src/Bookstore.Auth -- --environment Production
```

The design-time factory allows schema tooling without creating development
signing certificates or requiring demo credentials. The Production host refuses
missing, expired, or keyless certificate configuration; it never chooses
development signing credentials automatically. See OpenIddict's
[certificate guidance](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html).
The [Docker guide](docker-demo.md) documents the supported container run mode,
certificate preparation, discovery routing and production boundary.

## Verification

At this OAuth checkpoint, package restore and build passed with zero warnings/errors.
All 97 then-existing unit-test cases passed, including client permissions, scope/claim
rules, address validation, and safe Auth exception handling. EF reported no pending
authentication model changes.

The local HTTPS server was exercised against SQL Server, with normal certificate
validation in both an HTTP client and headless Microsoft Edge. Checks covered
discovery/public keys, real client-credentials and implicit token issuance,
cryptographic JWT signatures, audience/scope/expiry, rejected credentials and
grants/scopes, callback restrictions, form antiforgery, local return URLs, login,
reauthentication, logout, state mismatch, and callback fragment cleanup. Auth logs
were checked for the actual credentials and issued tokens; none were found.

Repeat Development startup preserved complete user/client records, including
password/secret hashes and redirect registrations, even when seed settings were
changed for the check. Public signing keys survived restart. The setup helper
also preserved existing user-secret values. Plain HTTP was rejected.

Production migrations and startup were checked using an isolated SQL Server
database. The host refused missing signing credentials, then started with
explicitly supplied temporary test certificates. It left users/clients empty and
returned 404 for the demo page. An idempotent Production SQL script was generated.
The test database and certificate files were removed; actual Auth user/client
records and existing Bookstore data remained unchanged.

These checks describe the OAuth server checkpoint. Subsequent API token enforcement,
cross-scope 401/403 checks and Swagger's callback are documented in the
[API security guide](api-security-swagger.md#verification). Container checks are
recorded separately in the [Docker guide](docker-demo.md#verification). Visual Studio
UI startup remains unverified. No integration-test project or package was added.

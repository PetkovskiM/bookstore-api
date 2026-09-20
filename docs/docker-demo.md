# Full Docker demonstration

The `demo` Compose profile runs the API, Auth, and the existing SQL Server
Developer service. Both applications use Development mode. SQL-only Compose
remains available for Visual Studio. This is the assignment's local demonstration,
not a production deployment configuration.

## Prepare once on Windows

This recommended Docker path needs only Git, Docker Desktop with Linux containers,
and Windows PowerShell. It does not use a host .NET SDK, Visual Studio, User Secrets,
or `dotnet dev-certs`. Start Docker Desktop, then run this as the normal Windows user:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-DockerDemo.ps1 -TrustHttpsCertificate
```

On a fresh clone, the helper creates the ignored `.env` with only
`MSSQL_SA_PASSWORD` and `MSSQL_PORT`, plus `.local/docker` settings, a localhost
HTTPS PFX/public certificate, and separate OAuth signing/encryption PFX files. It
generates all passwords and secrets randomly and never prints them. The explicit
switch trusts the generated public localhost certificate in the current user's
Windows trust store; omit it only if browser certificate warnings are acceptable.

On a repeat run, the helper validates and preserves a complete matching `.env` and
`.local/docker` configuration. It never rotates credentials or keys. A missing,
placeholder, partial, or mismatched file produces a short error instead; restore
the matching ignored files before continuing. If existing Docker demo volumes are
found without their matching runtime files, it also stops rather than generating a
new SQL password. It does not read or change User Secrets, database records,
containers, or volumes.

| Runtime file | Purpose |
| --- | --- |
| `.local/docker/api.json` | API SQL connection and HTTPS certificate configuration. |
| `.local/docker/auth.json` | Auth SQL connection, issuer/callbacks, demo credentials, and certificate configuration. |
| `.local/docker/https/localhost.pfx` | Private HTTPS certificate shared by the two local application listeners. |
| `.local/docker/https/localhost.crt` | Public HTTPS certificate trusted by container health checks and API discovery. |
| `.local/docker/oauth/signing.pfx` | Auth JWT signing key. |
| `.local/docker/oauth/encryption.pfx` | Auth encryption key; also protects persisted cookie keys. |

Keep this directory private and out of Git. The tracked
[API](../docker/api.settings.example.json) and
[Auth](../docker/auth.settings.example.json) examples contain placeholders only.
The helper is Windows-specific. Other systems can prepare the same file layout
and certificates manually, but that setup has not been verified.

## Run and switch modes

Stop local/Visual Studio API and Auth processes to free ports 7100 and 7200, then:

```powershell
docker compose --profile demo up -d --build --wait
docker compose --profile demo ps
```

The first build downloads the pinned .NET SDK/runtime images and restores NuGet
packages. Both Dockerfiles use a separate build stage and run the published
application as the image's non-root `app` user. SQL Server starts first; Auth waits
for SQL health, and the API waits for both SQL and Auth health. Development startup
applies each application's existing migrations and idempotent seeding.

The browser URLs are unchanged:

- Swagger: `https://localhost:7100/swagger`
- Auth account/login: `https://localhost:7200/`
- Discovery: `https://localhost:7200/.well-known/openid-configuration`
- Readiness: `https://localhost:7100/health` and `https://localhost:7200/health`

Follow the [Swagger and management-token demonstration](api-security-swagger.md).
Its Docker PowerShell token command reads the ignored `auth.json` file, not User
Secrets. CRUD requires `books.manage`; search requires the real implicit flow and
`books.search`.
Swagger uses the existing callback
`https://localhost:7100/swagger/oauth2-redirect.html`; the standalone Auth demo
still uses `https://localhost:7200/demo/callback`.

To return to optional local/Visual Studio development while keeping SQL running,
complete its User Secrets setup first:

```powershell
docker compose --profile demo stop api auth
docker compose up -d --wait sqlserver
dotnet run --project src/Bookstore.Auth --launch-profile https
# In another terminal:
dotnet run --project src/Bookstore.Api --launch-profile https
```

Use one application run mode at a time. Obtain fresh tokens and sign in again after
switching modes: the local Windows host and Docker have separate signing and cookie
keys. The databases remain shared, so do not alternate modes after either has seeded
Auth unless the retained local credentials/configuration match those existing records.
The seed intentionally preserves established users and clients rather than rotating
them.

## HTTPS and issuer decisions

Kestrel terminates HTTPS in each application container, using certificates mounted
read-only at runtime. There is no reverse proxy or plaintext application listener.
Only loopback host ports are published, including the existing SQL port. No
Windows hosts-file entry, external DNS provider or cloud authentication is needed.
This follows the [ASP.NET Core container HTTPS model](https://learn.microsoft.com/en-us/aspnet/core/security/docker-compose-https?view=aspnetcore-10.0).

The issuer stays exactly `https://localhost:7200/` for browsers, tokens, discovery
and API validation. Inside a container, `localhost` normally refers to that
container, so Compose sets the API's optional `Authentication:BackchannelHost` to
`auth`. A `SocketsHttpHandler.ConnectCallback` routes only the configured issuer's
TCP connection to that Docker service, on the same port. The HTTP URL, TLS server
name, certificate validation, and JWT issuer/audience checks remain unchanged.
Unexpected discovery origins and HTTP redirects are rejected in this mode.
See the [.NET connection callback](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.connectcallback?view=net-10.0).

`SSL_CERT_FILE` gives the API the exported public localhost certificate for trust;
it does not disable HTTPS verification. Health checks use curl with `--cacert`,
never `--insecure`. Browser/Windows trust is optionally added by
`-TrustHttpsCertificate`. The backchannel override is absent in the normal Visual
Studio configuration.
The fixed application ports are intentional: changing them also requires updating
issuer, callback registration, listener, health-check and routing configuration.

Auth accepts explicitly configured signing/encryption PFX files in Development for
Docker. Optional local development uses its existing Windows development certificates
when neither file is configured. Partial explicit configuration fails; Production
always requires explicit credentials and never falls back to development keys. HTTPS
and OAuth signing certificates serve different purposes and are kept separate.

## Persistence, health and secrets

`bookstore_sqlserver-data` retains both `Bookstore` and `BookstoreAuth`, including
migrations, books, authors, users and clients. Its name and mount are unchanged.
`bookstore_auth-data-protection` retains Auth's Data Protection keys. They are
encrypted with the mounted encryption certificate, so Identity cookies remain
readable after container recreation. Keep that certificate with its password and
key volume; losing it can invalidate existing sessions.

Both `/health` endpoints allow anonymous HTTPS requests and check database
connectivity. They return only `Healthy` or `Unhealthy` (200/503), with no database
or credential details. They are excluded from Swagger. The Compose SQL health
check still executes `SELECT 1`. Health depends on the database; a successful
container process start alone is not considered readiness.

Normal stops, restarts, and application recreation preserve these stores:

```powershell
docker compose --profile demo restart auth api
docker compose --profile demo up -d --no-deps --force-recreate --wait auth api
```

To inspect storage after a SQL restart without allowing startup seeding to obscure
the result, stop the two apps, restart SQL, query the existing rows, then start the
apps again. SQL's `SELECT 1` health check can succeed while a user database is still
recovering; allow a short bounded wait for both databases to be accessible before
comparing their rows. The applications also use SQL connection retries and their
own database health checks. Do not remove volumes or use `docker compose down -v`.

Compose mounts generated JSON files over each image's Development settings file.
Secrets are not build arguments or image layers; the Docker build context allows
only source/build inputs and excludes local settings, keys, caches, and assignment
files. Avoid printing `docker compose config` without `--quiet`: SQL's environment
contains its local password. Do not share private runtime files or authentication
headers. The API has no access to Auth's private signing/encryption certificates.

If a generated certificate expires or is lost, the helper stops rather than replacing
it. Restore the matching local runtime files or perform a deliberate, documented key
rotation after preserving the existing data. `-TrustHttpsCertificate` only trusts the
existing public localhost certificate; it does not generate or rotate certificates.
The generated demo certificates are valid for two years.

## Production boundary

Do not deploy this Development Compose profile as production. Use the same
application images with Production configuration, securely supplied runtime
settings and certificates, trusted public HTTPS names, and restricted database
logins. Provision clients/users deliberately and apply reviewed migrations before
startup. Production performs no automatic migration/demo seeding and exposes no
Swagger or standalone demo page.

Follow the README's [production limitations](../README.md#production-limitations) and
[Auth provisioning](oauth-server.md#controlled-production-setup). Set the exact
public issuer and callback URLs, configure API trust/authority, retain encrypted
Data Protection keys, and plan certificate/key rotation and backups. The local
`sa` login, self-signed HTTPS certificates and `TrustServerCertificate=True` SQL
setting are demonstration choices. A real production deployment is not verified
by this checkpoint. See the README's [production limitations](../README.md#production-limitations)
for the deployment boundary, Data Protection storage, and HTTPS reverse-proxy work.

## Verification

Local restore and build passed with zero warnings/errors, and all 140 unit tests
passed. Both Linux application images built successfully from the pinned SDK
`10.0.400` and runtime `10.0.12`.

Live checks used temporary book/Auth databases in the existing SQL Server container:

- All three services became healthy. Fresh migrations created both schemas and
  Development startup seeded 3 authors, 5 books, 1 user, and 2 OAuth clients.
- Normal HTTPS certificate validation passed in HTTP clients and headless Edge.
  Both real OAuth flows worked, including discovery/public keys, Swagger's popup
  callback, and actual search/management **Try it out** requests.
- CRUD, author conflicts, subtitle clearing, case-insensitive AND search,
  pagination, validation, missing/invalid-token 401s, and both scope-isolation
  directions passed. Existing Auth grant/callback/login/antiforgery checks passed.
- Both app containers ran as non-root users. After forced recreation, saved books,
  signing keys, previously issued management/search tokens, and the real Identity
  login cookie still worked. No sample data was duplicated or overwritten.
- After stopping the apps and normally restarting SQL Server, all book/author
  rows, users, clients and migration records matched their snapshots **before**
  app startup. Starting the apps again preserved them too.
- Earlier repeat preparation preserved its existing settings, certificates,
  User Secrets and `.env`. The current initializer instead validates/preserves its
  own ignored `.env` and `.local/docker` files without reading User Secrets.
  Container logs contained no actual SQL/demo/PFX passwords, management secret or JWT.

Both original databases remained unchanged during Docker verification. Only the
owned temporary databases were removed; all named volumes were retained. The
local application workflow was also rerun after these changes: both OAuth flows,
Swagger, scope checks and Production API protection still passed. Normal login
checks may advance Identity concurrency metadata without changing credentials.

These are the historical Docker-checkpoint results. The
[final verification record](final-verification.md) documents the later checks
against the existing local runtime without replacing settings, credentials, keys,
or volumes. A fresh Windows 10 Docker initialization/build/startup was subsequently
verified without a compatible local .NET SDK. Visual Studio's startup UI, a
non-Windows preparation workflow, and a deployed production environment have not
been exercised.

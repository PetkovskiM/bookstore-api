# Delivery and demonstration guide

This guide is the starting point for a reviewer. Use the [verification record](final-verification.md)
to distinguish checks completed on the development machine from checks still needed
in Visual Studio or on another Windows laptop. The assignment's required model,
flows, and our additional contract assumptions are listed in the
[README contract](../README.md#contract).

## Architecture and decisions

| Component | Responsibility and dependencies |
| --- | --- |
| `Bookstore.Api` | ASP.NET Core controllers bind/validate DTOs and enforce scopes. `BookService` uses `BookstoreDbContext` directly for async EF Core SQL Server operations. ProblemDetails and exception handlers provide consistent errors. |
| `Bookstore.Auth` | OpenIddict issues signed tokens. Minimal ASP.NET Core Identity supplies the demo user's password hashing, login/logout, secure cookies, and antiforgery checks. It has its own `AuthDbContext`. |
| SQL Server Developer | One container, separate `Bookstore` and `BookstoreAuth` databases, schema-only EF migrations, and the persistent `bookstore_sqlserver-data` volume. |
| Swagger | Development-only UI hosted by the API. The management bearer token comes from a PowerShell client-credentials request; SearchOAuth performs the real browser implicit redirect. |
| Unit tests and CI | The existing xUnit project checks application rules without an HTTP host or database. GitHub Actions restores, builds Release, and executes it using `global.json`. |

The API trusts the issuer's HTTPS discovery and public signing keys, not a shared
client secret. CRUD requires `books.manage`; search requires `books.search`.
`bookstore-management` is a confidential application; `bookstore-browser` is a
public application without a secret. They are not two human users.

Database-generated IDs, author reuse without renaming/merging, replacement PUT,
case-insensitive AND search, stable ID ordering, and pagination limits are explicit
implementation assumptions. DTOs keep the JSON contract separate from EF navigation
properties. A small service and DbContexts are sufficient; there is no repository
layer or separate author API. The required implicit flow remains implemented;
Authorization Code with PKCE is a future production browser-client improvement.

## Clean Windows laptop checklist

This is a Docker-only verification procedure, not a claim that the second-laptop
retest has already passed. CI already verifies restore, Release build, and unit tests,
so this checklist requires only Git, Docker Desktop with Linux containers, and Windows
PowerShell. Do not install the .NET SDK, Visual Studio, User Secrets, or development
certificates for this test. Use a fresh clone; do not copy another laptop's `.env`,
certificates, User Secrets, or `.local` directory. Record the commit, Windows/Docker
versions, result, and any failure at each step.

1. Start Docker Desktop and open Windows PowerShell at the repository root. The first
   image build downloads the existing container SDK/runtime images and NuGet packages.
   Run:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-DockerDemo.ps1 -TrustHttpsCertificate
   docker compose --profile demo config --quiet
   docker compose --profile demo up -d --build --wait
   docker compose --profile demo ps
   ```

   The initializer creates fresh ignored configuration only when none exists. On a
   repeat setup it validates/preserves matching files; do not work around an error by
   deleting a volume, changing a secret, or copying settings from another laptop.
2. Check HTTPS without bypassing certificate validation:

   ```powershell
   Invoke-RestMethod https://localhost:7100/health
   Invoke-RestMethod https://localhost:7200/health
   $discovery = Invoke-RestMethod https://localhost:7200/.well-known/openid-configuration
   $discovery | Select-Object issuer, token_endpoint, authorization_endpoint
   ```

   Both health responses must be `Healthy`; the issuer must be exactly
   `https://localhost:7200/`. Use `localhost` in browsers, not the Docker service
   name `auth` or the SQL address.
3. Deliberately place the generated demo-user password on the clipboard, then open
   `https://localhost:7100/swagger`:

   ```powershell
   (Get-Content -LiteralPath '.local/docker/auth.json' -Raw | ConvertFrom-Json).'DevelopmentDemo:UserPassword' | Set-Clipboard
   ```

   Follow the [presentation sequence](#presentation-sequence): obtain the management
   token using the Docker command, demonstrate CRUD, authorize SearchOAuth with
   `demo@bookstore.local`, and demonstrate search, 401, and both 403 directions.
4. Demonstrate persistence with a book you created. Keep the volumes, stop both
   applications, perform a normal SQL restart, then start the applications again:

   ```powershell
   docker compose --profile demo stop api auth
   docker compose restart sqlserver
   docker compose up -d --wait sqlserver
   # Wait for both user databases to be online before starting the applications.
   docker compose --profile demo up -d --no-build --wait
   ```

   Fetch the same book afterward and check that repeat startup did not duplicate or
   reset data. Record the result and delete only the temporary demonstration book.

## Presentation sequence

Allow roughly 8-10 minutes once the selected run mode is healthy. Use
`https://localhost:7100/swagger` and show only responses and non-sensitive metadata;
Swagger can display bearer tokens in generated curl commands, so avoid recording
those sections. The detailed [token instructions](api-security-swagger.md) cover
PowerShell acquisition, clipboard cleanup and browser login.

| Step | Demonstration and expected result |
| --- | --- |
| 1. Explain the three projects | Show the architecture table, generated IDs, author rules, and the distinction between required flows and implementation assumptions. |
| 2. Show readiness | All three Docker services healthy; API/Auth `/health` return `Healthy`; discovery publishes the exact HTTPS issuer. |
| 3. Show 401 | With neither Swagger scheme authorized, try search or GET by ID: 401 ProblemDetails and `WWW-Authenticate: Bearer`. |
| 4. Obtain management access | Run the client-credentials example in the security guide; show only `token_type`, `expires_in`, and `scope`. Authorize ManagementToken privately. |
| 5. Demonstrate CRUD | POST a uniquely titled book with a new author; note 201, both generated IDs, and Location. GET it; PUT with the returned author ID/name and omit `subTitle` to show it becomes null. Keep the book until the restart demonstration. |
| 6. Demonstrate implicit search | Authorize SearchOAuth as the demo user. Show the popup returning to Swagger, then search using a differently cased substring of your temporary title and author. Try page size 1, a page beyond the end, blank filters, and size 101 (400). `totalCount` is before pagination. |
| 7. Show scope isolation | Send the management token to search with the PowerShell snippet below: 403. Use the browser snippet for a search token on CRUD: also 403. Having both Swagger schemes authorized selects the correct token automatically, so explicit requests demonstrate the cross-scope failures. |
| 8. Show persistence and cleanup | Use the normal restart sequence above, read the same book, then DELETE only that demonstration book (204); GET now returns 404. Its author intentionally remains. Existing development data must stay untouched. |
| 9. Close with delivery boundaries | Show the green CI badge, passing local checks, the Production checklist, and any uncompleted clean-machine/IDE checks. |

For predictable 401/403 output in Windows PowerShell 5.1, use the management headers
already created by the token example and keep error handling from dumping request
details:

```powershell
try {
    Invoke-WebRequest -UseBasicParsing -Uri 'https://localhost:7100/api/books/search' -Headers $managementHeaders -ErrorAction Stop | Select-Object StatusCode
} catch {
    if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
    else { 'Request failed before an HTTP response; check local services and HTTPS trust.' }
}
# Expected: 403. Repeat without -Headers $managementHeaders for 401.
```

To demonstrate the other direction, authorize SearchOAuth in Swagger, then run
this in that page's browser developer console. It uses the token in memory and
prints only the HTTP status. ID `0` cannot be a generated book ID, but the scope
check runs before a database lookup, so the result is 403 rather than 404:

```javascript
(async () => {
    const token = window.ui.authSelectors.authorized()
        .getIn(['SearchOAuth', 'token', 'access_token']);
    if (!token) { console.log('Authorize SearchOAuth first.'); return; }
    const response = await fetch('/api/books/0', {
        headers: { Authorization: 'Bearer ' + token }
    });
    console.log(response.status);
})();
```

Do not inspect or print the authorization object/token. Reload Swagger to clear
its tokens after the demonstration; use Auth's **Sign out** to clear the separate
login session.

For a two-book pagination example, POST another book referencing the returned
author ID/name, with a title sharing the same unique prefix. Each page of size 1
should contain one book and report `totalCount: 2`. Delete only those demonstration
books afterward. Reusing the generated author prevents unnecessary duplicate authors.

## Production checklist

The provided Compose profile is a Development demonstration. These are deployment
requirements and remaining operational decisions, not a claim of a tested
production installation.

| Area | Required configuration or action |
| --- | --- |
| Environment and configuration | Set both applications explicitly to `Production`. Supply secrets through the deployment's protected runtime configuration. Do not deploy Development settings, local user-secrets stores, `.env`, `.local/docker`, the `sa` login, or demo credentials. |
| Database | Provision separate databases and restricted runtime logins. Use a separate deployment identity to review/apply idempotent EF SQL scripts. Back up first. Use trusted SQL TLS with `TrustServerCertificate=False`. Production startup does not migrate or seed. |
| Issuer and API trust | Set Auth's `Auth__Issuer` and API's `Authentication__Authority` to the same deployed HTTPS issuer, including the exact path/trailing slash. Set API `Authentication__Audience=bookstore-api`. Trust the issuer's HTTPS certificate on the API host. Remove the demo `Authentication__BackchannelHost`/localhost trust-file override unless deliberately required by the deployed network. |
| Clients and users | Provision through OpenIddict's application manager and Identity's `UserManager`. Current controllers recognize `bookstore-management` and `bookstore-browser`; preserve those IDs, their respective grants/scopes, and the public client's absence of a secret. Supply a new strong confidential-client secret. Register exact deployed browser callbacks and configure the matching `Auth:BrowserRedirectUris` list. No Production demo user/client seeding or registration UI exists. |
| Signing and encryption | Supply separate valid RSA signing/encryption PFX files and passwords using `Certificates__Signing__Path`, `Certificates__Signing__Password`, `Certificates__Encryption__Path`, and `Certificates__Encryption__Password`. These are separate from the HTTPS listener certificate. Production refuses missing signing configuration and never automatically generates development credentials. |
| Data Protection | Set `DataProtection__KeysPath` to persistent storage writable by Auth. The implementation sets application name `Bookstore.Auth` and encrypts cookie keys with the encryption certificate. Share the same key ring/application name across Auth instances. Preserve it and its decryption certificates/passwords together; key loss invalidates cookies. Rotation needs an explicit overlap/recovery plan, not replacement of the demo files. |
| HTTPS and hostnames | Configure HTTPS listeners with deployment certificates (for example `Kestrel__Certificates__Default__Path`/`Password`) and public DNS. Restrict `AllowedHosts` to deployed hostnames, restrict network access, and validate discovery, callbacks and generated Location URLs externally. Keep HTTPS verification enabled. |
| Client and operational readiness | Swagger and the standalone demo are disabled outside Development. Deploy/provision an appropriate real browser client and its callbacks. Plan monitoring without credentials/request bodies, database/key backups, recovery tests, patching, and protection against repeated abusive login/token requests. JWT logout is not immediate access-token revocation; access tokens remain usable until expiry. |

See the existing [database provisioning](../README.md#controlled-production-provisioning)
and [Auth provisioning](oauth-server.md#controlled-production-setup) commands. The
final review generated both idempotent scripts in memory and checked that they
contain schema, not sample books/users/clients. Applying a script to a production
database is a separate deployment action.

Kestrel currently receives HTTPS directly. There is no general reverse-proxy
forwarded-header configuration in the application. If a future proxy terminates
TLS and forwards plain HTTP, explicitly configure trusted proxies/networks and
forwarded scheme/host processing **before** the HTTPS gate, HSTS and authentication;
otherwise requests will be rejected or callback URLs can be wrong. Restrict allowed
hosts and forwarded-header trust to the actual deployment topology and test it.
The current demo does not verify that topology. See
[Microsoft's proxy guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Persistent key storage and encryption are distinct requirements; configure both.
See [Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)
and [OpenIddict credential guidance](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html)
for certificate separation and rotation considerations. A production browser
client should move to Authorization Code with PKCE in an explicitly planned
change; the assignment's implicit implementation is preserved here.

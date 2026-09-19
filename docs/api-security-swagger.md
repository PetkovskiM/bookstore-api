# API security and Swagger

The API accepts signed access tokens from `Bookstore.Auth`. CRUD requires
`books.manage`; `GET /api/books/search` requires `books.search`. These policies
apply to the server endpoints, including requests made outside Swagger.

## Start the local demonstration

Complete the [database setup](../README.md#local-setup) and
[Auth user-secrets setup](oauth-server.md#windows-and-visual-studio-setup) first.
Preserve existing `.env` and user-secrets values. From the repository root:

```powershell
docker compose up -d --wait sqlserver
dotnet dev-certs https --trust
dotnet run --project src/Bookstore.Auth --launch-profile https
# In a separate terminal:
dotnet run --project src/Bookstore.Api --launch-profile https
```

Open `https://localhost:7100/swagger`. Both applications now have only an `https`
launch profile. In Visual Studio 2026, configure both as startup projects with
that profile. Start Auth first when running in separate terminals; the API needs
its HTTPS discovery document and public signing keys to validate tokens.
Do not bypass certificate validation if discovery or login fails: trust the
development HTTPS certificate and use `localhost` for both application URLs.

Auth's form security policy permits its own origin and the HTTPS origins in
`Auth:BrowserRedirectUris`. Chromium checks that policy across the login POST's
redirect chain, so Swagger's origin must be included. OpenIddict still validates
the complete callback against the client's database registration; this header
does not authorize new OAuth callbacks. Keep the configured list consistent with
controlled client provisioning. See the browser behavior documented for
[CSP form-action](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/form-action).

Swagger and its OpenAPI document (`/swagger/v1/swagger.json`) are available only
in Development, without a token. Books remain protected in every environment.
No browser CORS policy is needed: Swagger calls the API on its own origin, and
implicit login uses a browser redirect to Auth.

## Management token and CRUD

Use PowerShell to obtain a token. The management client secret stays in local
user secrets and never goes into Swagger configuration or browser requests:

```powershell
$secretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\bookstore-auth-development\secrets.json'
$authSettings = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
$managementToken = Invoke-RestMethod -Method Post -Uri 'https://localhost:7200/connect/token' -Body @{
    grant_type = 'client_credentials'
    client_id = 'bookstore-management'
    client_secret = $authSettings.'DevelopmentDemo:ManagementClientSecret'
    scope = 'books.manage'
}
$managementHeaders = @{ Authorization = 'Bearer ' + $managementToken.access_token }
# Confirm non-sensitive response fields without printing the token:
$managementToken | Select-Object token_type, expires_in, scope
```

Use `$managementHeaders` with the [PowerShell CRUD walkthrough](../README.md#try-the-endpoints-from-powershell).
For Swagger, copy just the token locally with
`$managementToken.access_token | Set-Clipboard`, open **Authorize**, paste into
**ManagementToken**, and click its **Authorize** button. Do not include the
`Bearer ` prefix or paste the client secret. Clear the clipboard afterward with
`Set-Clipboard -Value ''`; do not save/share screenshots of tokens or generated
authenticated curl commands. Close the dialog and use CRUD's **Try it out**.

Start with POST's **newAuthor** example. Use the returned IDs and exact author name
when trying **existingAuthor**, GET, PUT or DELETE. Example ID `1` is illustrative;
it is not a guarantee about your database. Omitting `authorId` creates a new author,
even when its name exists. Reusing an unknown ID returns 400; a name mismatch
returns 409. POST rejects a supplied `bookId`. PUT replaces editable fields, clears
an omitted/null `subTitle`, and returns 404 rather than upserting a missing book.
Deleting a book intentionally retains its author.

## Implicit login and search

1. Open Swagger's **Authorize** dialog and find **SearchOAuth**. The client ID is
   `bookstore-browser`, with `books.search` selected. There is no client secret.
2. Click its **Authorize** button. Allow the local login popup if your browser
   blocks it. Sign in as `demo@bookstore.local` with the password in **Bookstore.Auth
   > Manage User Secrets**, under `DevelopmentDemo:UserPassword`.
3. The popup returns to exactly
   `https://localhost:7100/swagger/oauth2-redirect.html` and closes. Close the dialog
   and execute `GET /api/books/search` using **Try it out**.
4. Try title `dUnE`, author `HERBERT`, page number `1`, and page size `1`. Filters
   are case-insensitive substrings combined with AND. Blank filters are ignored.
   Results are ordered by `bookId`, with `totalCount` before pagination. Defaults
   are page 1 and size 10; size must be 1–100. No matches or a page beyond the end
   returns 200 with empty `items`.

Swagger chooses the matching token for each operation when both are authorized.
It keeps tokens in page memory, with persistence disabled; reloading clears the
Swagger authorization state. Its online schema validator is disabled. The Auth
login cookie is separate: to clear that session, visit `https://localhost:7200/`
and use **Sign out**. Swagger's **Logout** clears its token, not the Auth cookie.
Existing access tokens remain valid until expiry even after Identity logout.

Implicit is implemented explicitly because the assignment requires it. For a
production browser client, Authorization Code with PKCE is the recommended
improvement; this branch does not change the required grant. See
[OpenIddict's flow guidance](https://documentation.openiddict.com/guides/choosing-the-right-flow.html).

## Why the API trusts or rejects a request

`JwtBearer` retrieves discovery metadata/public keys from the configured HTTPS
authority. It requires an RSA SHA-256 signature from the issuer, the expected
issuer and `bookstore-api` audience, an `at+jwt` access-token type, and a valid
expiry/not-before window. A 30-second clock allowance handles small clock differences.
Unsigned tokens, other signing algorithms and ordinary identity tokens are rejected.
The API never needs the issuer's private signing key or the management client secret.
See [Microsoft's JWT validation guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).

After validation, each action checks its exact, case-sensitive scope. The scope
helper supports space-separated values and repeated scope claims; it never uses
substring matching. A fallback policy also requires authentication on routes
without an explicit policy. Development Swagger middleware runs before that gate.

| Request | Result |
| --- | --- |
| No token, malformed token, bad signature, wrong issuer/audience, or expired token | 401 ProblemDetails with `WWW-Authenticate: Bearer` |
| Valid search token on any CRUD operation | 403 ProblemDetails |
| Valid management token on search | 403 ProblemDetails |
| Valid token with the required scope | Normal operation/validation response |
| Plain HTTP, even if a listener is explicitly configured | 400; no credential-bearing redirect |

For a quick manual cross-scope check, send `$managementHeaders` to
`https://localhost:7100/api/books/search`: expect 403. With neither scheme authorized
in Swagger, executing any book operation returns 401. The real-token verification
also checks every CRUD operation using a search token.

Authentication error details are suppressed in challenges. Errors use the existing
ProblemDetails pipeline with a trace ID. Authentication information/debug logging
is suppressed because failures can contain token details; sensitive EF logging
remains disabled. No credential or token is written to tracked configuration.

## Configuration and deployment limits

Development config sets `Authentication:Authority` to `https://localhost:7200/`.
The base config deliberately leaves it blank. Outside Development, explicitly set
`Authentication__Authority` to the deployed HTTPS issuer (use discovery's exact
issuer, including its trailing slash), and `Authentication__Audience` to
`bookstore-api`. Missing/invalid settings fail startup. The issuer's TLS certificate
must be trusted by the API host; HTTPS metadata validation is never disabled.

Production has no Swagger, automatic migrations or sample seeding. Keep the
[controlled database provisioning](../README.md#controlled-production-provisioning)
and [Auth signing/client provisioning](oauth-server.md#controlled-production-setup)
steps. JWT validation uses cached discovery keys and does not query the Auth
database on every request; immediate token revocation is not implemented.

The supported run mode for this checkpoint is local applications with containerized
SQL Server. Full application Compose, issuer/hostname/proxy configuration, and
certificate mounts belong to `feat/docker-demo`. CI and integration tests remain
outside this branch.

## Verification

Restore and build passed without warnings/errors; all 131 unit-test cases passed.
The added tests cover exact scope enforcement on every book action and token
rejection for wrong issuer, audience, signature, lifetime, type and algorithm,
missing expiry, and unsigned tokens. These unit-generated tokens check validation
rules; they do not stand in for real OAuth issuance.

Additional tests serialize the OpenAPI document to check each operation's security
requirement and ProblemDetails media type, and verify the Auth form-policy allowlist.

Live checks used a temporary SQL Server book database and the real local Auth
server, with normal HTTPS certificate validation in both an HTTP client and
headless Microsoft Edge. They passed for:

- Both real OAuth grants, HTTPS discovery/public keys, the registered Swagger
  callback and actual Swagger **Try it out** for search and management.
- 401 ProblemDetails on every book operation without a token, and on malformed
  or tampered tokens; 403 for search tokens on every CRUD operation and for
  management tokens on search. Rejected writes left the catalog unchanged.
- Authorized CRUD, generated IDs/Location, author conflicts, subtitle clearing,
  case-insensitive AND search, pagination, and validation errors.
- Swagger's serialized schemas/examples/security requirements, cleared authorization
  after reload, and no token persistence or external browser requests.
- Existing Auth checks for forbidden grants/scopes, unregistered callbacks,
  antiforgery, login, reauthentication, callback state handling and logout.
- Repeated Development API startup without duplicate books. Production still
  enforced tokens/scopes, disabled Swagger and rejected HTTP. The framework
  excludes localhost from its Production HSTS header by default.

EF reported no pending API model changes; no migration was needed. Logs were
checked for actual credentials and issued tokens. Original Bookstore rows,
Auth credentials and client registrations were preserved; login attempts can
advance Identity's concurrency metadata. Verification stopped its own processes
and removed only the temporary book database, leaving Docker volumes intact.

Wrong issuer/audience, expired/not-yet-valid tokens, missing expiry, and unsupported
token types/algorithms were checked by the cryptographic unit tests; live API
checks used genuine issued tokens and malformed/tampered tokens. Visual Studio's
startup UI and full application containers have not been exercised. SQL Server
container-restart persistence was verified in the earlier persistence checkpoint,
not repeated here. No integration-test project or packages were added.

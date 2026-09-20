# Bookstore API

[![Build and tests](https://github.com/PetkovskiM/bookstore-api/actions/workflows/build-and-tests.yml/badge.svg?branch=main)](https://github.com/PetkovskiM/bookstore-api/actions/workflows/build-and-tests.yml)

An ASP.NET Core bookstore API. The solution setup, Book/Author contracts and
validation, unit tests, EF Core SQL Server persistence, book CRUD, paginated search,
and a local OAuth authorization server with API scope enforcement and Swagger are implemented.
The recommended reviewer path is the complete Docker Compose demonstration. GitHub
Actions verifies restore, Release build, and unit tests on pull requests and pushes to
`main`.

Start with the [delivery guide](docs/delivery-guide.md) for the architecture,
clean Windows setup, presentation sequence, and Production checklist. The
[final verification record](docs/final-verification.md) separates completed checks
from the Docker clean-laptop retest and optional Visual Studio checks still to be performed.

## Docker quick start (recommended)

Requires only Git, Docker Desktop running Linux containers, and Windows PowerShell.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-DockerDemo.ps1 -TrustHttpsCertificate
docker compose --profile demo up -d --build --wait
```

- Swagger: `https://localhost:7100/swagger`
- Auth: `https://localhost:7200/`
- Health: `https://localhost:7100/health` and `https://localhost:7200/health`

```powershell
(Get-Content -LiteralPath '.local/docker/auth.json' -Raw | ConvertFrom-Json).'DevelopmentDemo:UserPassword' | Set-Clipboard
```

Open Swagger, obtain the management token with the [Docker token command](docs/api-security-swagger.md#management-token-and-crud), authorize **ManagementToken**, and try a CRUD operation. Then authorize **SearchOAuth**, sign in as `demo@bookstore.local` using the clipboard password, and try search.

## Solution

Open `Bookstore.slnx`, the solution containing all three .NET 10 projects:

| Project | Purpose |
| --- | --- |
| `src/Bookstore.Api` | Protected book CRUD/search, Swagger, contracts, ProblemDetails, EF Core migrations, and development sample data. |
| `src/Bookstore.Auth` | OpenIddict issuer, minimal Identity login/logout, separate authentication database, and a Development browser flow check. |
| `tests/Bookstore.UnitTests` | xUnit tests for validation, pagination, JSON contracts, author rules, OAuth client/claim rules, API token/scope validation, errors, and safe logging. |

## Optional local / Visual Studio development

This path is not required for the Docker quick start. It requires local .NET tooling,
Visual Studio (if used), and User Secrets.

### Prerequisites and Visual Studio

- Install the stable .NET SDK `10.0.400`. `global.json` allows newer stable
  `10.0.4xx` patches and disallows previews.
- Use Visual Studio 2026 with the **ASP.NET and web development** workload.
  Version 18.9 or later is recommended for this SDK. Microsoft lists 18.0 as
  the minimum for targeting .NET 10; Visual Studio 2022 is not compatible.
  See the [SDK/Visual Studio compatibility table](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).
- In Visual Studio 2026 (the installed Insiders edition is suitable), choose
  **File > Open > Project/Solution**, select
  `D:\GIT\bookstore-api\Bookstore.slnx` (or your clone's equivalent), then choose
  **Build > Build Solution**. The `.slnx` extension is the XML solution format.

Local review found SDKs `5.0.408`, `6.0.428`, `8.0.100`, `10.0.204`, and
`10.0.400`; `dotnet --version` selects the stable `10.0.400` SDK in this repository.
Discovery with `vswhere -all -prerelease -products *` found both Visual Studio
Enterprise 2022 `17.8.3` and Community 2026 Insiders `18.10.12106.202`, each with
the ASP.NET and web development workload. Insiders is installed at
`C:\Program Files\Microsoft Visual Studio\18\Insiders` and meets the .NET 10
version requirement. The earlier inspection omitted prerelease installations.
The SDK remains stable even when using the Insiders IDE. Opening, building, and
debugging in that IDE have not yet been verified.

### Build, test, and run locally

From the repository root in PowerShell:

```powershell
dotnet --version
dotnet restore Bookstore.slnx
dotnet build Bookstore.slnx --configuration Release --no-restore
dotnet test tests/Bookstore.UnitTests/Bookstore.UnitTests.csproj --configuration Release --no-build --no-restore
```

The first command should report `10.0.400` or a newer stable `10.0.4xx` patch.
Restore downloads EF Core and the test packages from NuGet; no database is needed to build or
run these unit tests. The validation tests call MVC's object validator directly,
including its validation of nested authors. JSON tests use the web serializer
defaults. No HTTP server, database, or integration-test packages are involved.

Verified locally for the final review with SDK `10.0.400`: package restore passed,
Release build passed with zero warnings/errors, all 140 unit-test cases passed,
and `dotnet format Bookstore.slnx --verify-no-changes --no-restore` passed.
See the [API security/Swagger guide](docs/api-security-swagger.md) and
[OAuth server guide](docs/oauth-server.md#verification) for live checks.
Database checks are recorded below; the unit tests themselves use no database.

Complete the optional local database setup below before starting the API and the
[optional Auth setup](docs/oauth-server.md#optional-local--visual-studio-development)
before starting Auth. Use separate terminals:

```powershell
dotnet run --project src/Bookstore.Auth --launch-profile https
dotnet run --project src/Bookstore.Api --launch-profile https
```

The API uses `https://localhost:7100`; Auth uses `https://localhost:7200`.
Open `https://localhost:7100/swagger` for the Development demonstration UI.
The API serves the protected CRUD and search routes below. Its fallback policy
requires a token for other routes too; unknown routes return 404 after authentication.
Auth serves discovery, token issuance, and login/logout; its Development browser
check is at `https://localhost:7200/demo`.
In Development, API startup applies migrations and initializes an empty catalog.
In Visual Studio, configure both projects as startup projects with their `https` profiles.
Trust the local development certificate with `dotnet dev-certs https --trust`.
Both applications require HTTPS and reject plaintext HTTP instead of redirecting
requests containing credentials. Visual Studio UI startup remains unverified.

### Docker run mode

Use the [Docker quick start](#docker-quick-start-recommended) instead of this
optional local path. Stop local API/Auth processes before Docker starts because the
same HTTPS ports are used. The full [Docker demonstration guide](docs/docker-demo.md)
covers storage, certificate handling, switching modes, and troubleshooting. Images
contain no private certificates or populated local settings, and both applications
run as non-root users. `/health` is anonymous and checks database connectivity.

## Continuous integration

The [Build and tests workflow](.github/workflows/build-and-tests.yml) runs for pull
requests targeting `main` and pushes to `main`. It installs the SDK selected by
`global.json` and runs the same restore, Release build, and unit-test commands
listed above, on `ubuntu-latest` with read-only repository permissions.

It needs no SQL Server, User Secrets, `.env`, authentication credentials, generated
certificates, or Docker. The developer confirmed that both the CI pull-request run
and post-merge `main` run passed, with a green README badge. The badge and link use
the exact workflow path `.github/workflows/build-and-tests.yml`.

CI covers the existing unit tests. SQL Server, actual OAuth issuance, browsers,
and a clean Windows setup require the separate checks in the
[verification record](docs/final-verification.md).

## Book CRUD

All four CRUD operations require a bearer token obtained through client credentials
with `books.manage`. Missing/invalid tokens return 401; search tokens return 403.

| Method and route | Success | Behavior |
| --- | --- | --- |
| `POST /api/books` | 201 | Returns the created book and a `Location` header pointing to its GET route. |
| `GET /api/books/{bookId}` | 200 | Returns one book with its nested author. |
| `PUT /api/books/{bookId}` | 200 | Replaces title, author reference, and subtitle; returns the updated book. |
| `DELETE /api/books/{bookId}` | 204 | Deletes that book; the response body is empty and the author remains. |

IDs in routes are integers. Missing books return 404; PUT never creates a missing
book. Creation rejects a supplied `bookId`, including `0` or `null`. PUT always
uses the route ID; an extra body `bookId` is ignored like other unmapped fields.
Missing/null subtitles clear the stored subtitle on PUT.

For both writes, omitting `authorId` creates a new author, even if another author
has the same name. Supplying an existing ID reuses that author. The supplied name
must match the stored name after trimming, using an ordinal, case-sensitive
comparison. Unknown author IDs return a 400 validation problem; mismatched names
return 409. Neither operation renames a shared author. There is no author API.

`BooksController` handles routes and HTTP responses. `BookService` uses the
DbContext directly, with async database operations and request cancellation
tokens. `AuthorRules` holds the rule shared by creation and replacement so it can
be tested without a database. A new author and its book are saved in one EF
transaction. DTO responses prevent database navigation properties from leaking
into JSON or producing reference cycles.

### Try the endpoints from PowerShell

Complete the [Docker quick start](#docker-quick-start-recommended) or the
[optional local database setup](#optional-local-database-setup), then obtain
`$managementHeaders` using the [management-token example](docs/api-security-swagger.md#management-token-and-crud).
Then run in the same PowerShell terminal:

```powershell
$booksUrl = 'https://localhost:7100/api/books'
$creation = @{
    title = '  Demo Book  '
    author = @{ name = 'Demo Author' }
    subTitle = 'First edition'
} | ConvertTo-Json

$created = Invoke-RestMethod -Method Post -Uri $booksUrl -Headers $managementHeaders -ContentType 'application/json' -Body $creation
$bookUrl = "$booksUrl/$($created.bookId)"
Invoke-RestMethod -Uri $bookUrl -Headers $managementHeaders

$replacement = @{
    title = 'Updated Demo Book'
    author = @{ authorId = $created.author.authorId; name = $created.author.name }
} | ConvertTo-Json

# Omitting subTitle clears it. The book ID and author ID stay the same here.
Invoke-RestMethod -Method Put -Uri $bookUrl -Headers $managementHeaders -ContentType 'application/json' -Body $replacement
Invoke-WebRequest -UseBasicParsing -Method Delete -Uri $bookUrl -Headers $managementHeaders | Select-Object StatusCode
```

This walkthrough creates one author. Deleting the demo book intentionally leaves
that author in the database. Use IDs returned by the API rather than assuming a
particular seed ID.

### Errors and checks

Errors use `application/problem+json`, including a status, title, type, request
path in `instance`, and a `traceId`. Validation failures also include an `errors`
dictionary; an unknown author is reported under `Author.AuthorId`. Invalid JSON,
missing required fields, and failed length checks return 400. Unsupported media
types return 415; unsupported methods return 405. Unknown routes return 404 after
authentication. Rejections with 401/403 also use ProblemDetails; 401 includes a
`WWW-Authenticate: Bearer` header without token-validation details.

`AddProblemDetails()` and `IExceptionHandler` handle errors in both Development
and Production. Unexpected exceptions return a generic 500; the handler logs
only the exception type and trace ID. Raw exception messages, request bodies,
headers, and connection strings are not included in that log or error response.
ASP.NET Core's raw exception diagnostics are suppressed in favor of that safe log.
See [ASP.NET Core error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0).

The CRUD checkpoint passed 66 unit tests and 45 live HTTP requests against a
temporary, isolated SQL Server database. Those HTTP checks covered all four
operations, generated IDs and Location, trimming and length boundaries, long
subtitles, author creation/reuse/conflicts, failed writes leaving data unchanged,
PUT replacement and subtitle clearing, missing books, deletion retaining authors,
ProblemDetails (400/404/405/409/415/500), and persistence across an API restart.
The temporary database was removed; the existing `Bookstore` rows and migration
history were compared before/after and remained unchanged. No integration-test
project or additional test package was added. Search was verified in the following
checkpoint. Those historical checks predate authentication; current token and
Swagger checks are in the [security guide](docs/api-security-swagger.md#verification).

## Book search

`GET /api/books/search` returns matching books and their authors. It requires
`books.search` from the implicit flow. Management tokens return 403 on this route.

| Query parameter | Behavior |
| --- | --- |
| `title` | Optional case-insensitive substring of the book title. |
| `author` | Optional case-insensitive substring of the author's name. |
| `pageNumber` | Positive integer; defaults to 1. |
| `pageSize` | Integer from 1 to 100; defaults to 10. |

Filters are trimmed; blank filters are ignored. Supplying both filters requires
both to match (AND). Omitting both lists all books, paginated. Search text is
literal: `%`, `_`, and `[` do not act as wildcards. The response contains `items`,
`totalCount` (all matches before pagination), `pageNumber`, and `pageSize`.
Items use the same nested author and `subTitle` spelling as GET by ID.

Books are always ordered by `bookId` before pagination. No matches or a page
beyond the last match returns 200 with an empty `items` array; the latter still
reports the matching `totalCount`. Invalid or non-integer pagination returns 400
with validation ProblemDetails. Large valid page numbers also return an empty
page: the offset is calculated with a `long` so multiplication cannot wrap around.

With both applications running, [authorize SearchOAuth in Swagger](docs/api-security-swagger.md#implicit-login-and-search)
and use **Try it out** on `GET /api/books/search`. Try `title=dUnE`,
`author=HERBERT`, `pageNumber=1`, and `pageSize=1`; repeat with page 2.
An unchanged sample catalog has two matching books, so each page has one item and
`totalCount: 2`. Try page `2147483647` for an empty page and size `101` for a 400.

`BooksController` binds query parameters and uses the existing MVC validation.
`BookService` composes the filters, counts the matches, and selects only the
requested page and response fields in SQL. Reads are asynchronous, accept request
cancellation, and do not track entities. Count and page are separate reads, so
concurrent catalog changes can affect counts or the contents of later pages.

The search expressions explicitly use SQL Server's `Latin1_General_100_CI_AS_SC`
collation (text comparison rules). It ignores case, distinguishes accents, and
supports supplementary Unicode characters. This implementation choice guarantees
case-insensitive search even when the database defaults to case-sensitive
comparisons; the PDF does not specify a collation or accent handling. No schema
change or migration is needed. Explicit query collations can limit index use;
this small catalog does not yet need additional search indexes. See
[EF Core collation guidance](https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity).

### Search verification

The search checkpoint passed all 71 unit-test cases, including offset boundaries
that exceed `int.MaxValue`, and 79 live HTTP requests against a temporary SQL
Server database. That database deliberately used a case-sensitive default;
ordinary SQL title comparisons were confirmed case-sensitive before checking
that the API still matched titles and authors without regard to case.

The HTTP checks covered trimmed/blank/combined filters, literal wildcard and quote
characters, Unicode, short and long filters, defaults, full/partial/empty pages,
maximum page size, very large page numbers, counts and ID ordering, response
fields, invalid query parameters and ProblemDetails, and changes made through
CRUD appearing in search. An API restart preserved the rows and search results
without duplicate seeding. An empty database returned an empty page in Production.

EF reported no pending model changes. The temporary database was removed and the
existing `Bookstore` rows and migration history were confirmed unchanged. These
were manual SQL Server checks; no integration-test project or package was added.
Those historical checks predate authentication; see the
[security guide](docs/api-security-swagger.md#verification) for current checks.

## OAuth authorization server

`Bookstore.Auth` now provides local OpenIddict token issuance and minimal Identity
login/logout over HTTPS. It uses `BookstoreAuth`, a separate database in the same
SQL Server container. The API validates its signed access tokens and enforces a
different scope for CRUD and search.

| Client | Flow | Permitted scope |
| --- | --- | --- |
| `bookstore-management` (confidential, with a secret) | Client credentials | `books.manage` |
| `bookstore-browser` (public, without a secret) | Implicit | `books.search` |

The two clients are applications, not two users. The single Development user is
`demo@bookstore.local`. The Docker initializer generates its password and the
management secret in ignored `.local/docker/auth.json`; optional local development
uses Auth User Secrets through `scripts/Initialize-AuthDevelopment.ps1`. Existing
settings, passwords, and client registrations are preserved on repeated startup.
No credentials or demonstration rows are included in migrations or tracked
configuration. Production does not migrate or seed automatically and requires
explicit signing/encryption credentials.

See the [OAuth server guide](docs/oauth-server.md) for setup, both flow examples,
exact callback URLs, certificate and token decisions, production provisioning,
and checks performed. The Development browser callback has been exercised in
Edge. The [API security/Swagger guide](docs/api-security-swagger.md) explains token
validation, the Swagger callback, management-token setup, and 401/403 behavior.

## SQL Server persistence

### Optional local database setup

Use Docker Desktop with Linux containers. Compose runs SQL Server 2022 Developer
on `127.0.0.1,14333` by default, bound only to the local machine. The API connects
to the `Bookstore` database. Auth uses the separate `BookstoreAuth` database on
the same server and persistent volume; see its setup guide above.

1. The Docker quick start creates `.env` automatically. For this optional local path,
   copy the placeholder once with `Copy-Item .env.example .env` only when `.env` is
   absent. If it already exists, preserve it. Set `MSSQL_SA_PASSWORD` to a strong
   local password (at least 8 characters, including upper/lowercase letters, digits,
   and a symbol). `.env` is ignored; only `.env.example` is tracked.
2. Run `docker compose up -d --wait sqlserver`, then `docker compose ps`.
   The health check makes a real SQL connection and executes `SELECT 1`.
3. In Visual Studio, right-click **Bookstore.Api > Manage User Secrets** and add
   the following entry, substituting the same local password. Preserve any other
   secrets already in that file. If you change `MSSQL_PORT`, change the connection
   string's port as well.

```json
{
  "ConnectionStrings:Bookstore": "Server=tcp:127.0.0.1,14333;Database=Bookstore;User Id=sa;Password=<your-local-password>;Encrypt=True;TrustServerCertificate=True"
}
```

User secrets live outside the repository. The tracked `appsettings.json` contains
an empty `ConnectionStrings:Bookstore` placeholder. For a shell or deployment,
`ConnectionStrings__Bookstore` is the equivalent environment variable; `.env` is
read by Compose, not automatically by ASP.NET Core. The explicit IPv4 TCP address
matches Compose's local binding; `localhost` timed out during local SQL client
verification. `TrustServerCertificate=True`
accepts the local container's self-signed SQL certificate for development.
The `sa` login is only a local setup convenience; use separate deployment and
restricted runtime logins in production. Sensitive EF logging is left disabled.
Do not print secrets, full connection strings, or expanded Compose configuration.

From the repository root:

```powershell
dotnet tool restore
dotnet ef database update --project src/Bookstore.Api -- --environment Development
dotnet run --project src/Bookstore.Api --launch-profile https
```

The local `dotnet-ef` tool and EF packages are pinned to `10.0.12`. The migration
command creates `Bookstore` and applies `InitialCreate`; in Development it also
initializes sample data. Starting the API with its Development launch profile
performs the same migration/seeding automatically, so the explicit update command
is optional. SQL Server must be healthy first. Build and unit tests need neither
a connection string nor a running database. Missing database configuration gives
a setup error when the DbContext is resolved; it never falls back to memory.

### Model and relationship decisions

`Data/BookstoreDbContext.cs` maps the existing models to `Authors` and `Books`.
The PDF's required fields and limits become database constraints:

| Column | SQL Server mapping |
| --- | --- |
| `Authors.AuthorId`, `Books.BookId` | `int IDENTITY(1,1)` primary keys, generated by SQL Server |
| `Authors.Name`, `Books.Title` | Required `nvarchar(100)`, with minimum-length check constraints |
| `Books.AuthorId` | Required `int` foreign key to `Authors.AuthorId`, with an index |
| `Books.SubTitle` | Nullable `nvarchar(max)`, with no invented assignment length limit |

`Author.Books` is the collection side of the one-to-many relationship; `Book.Author`
and `Book.AuthorId` point to its required author. Several books can share one
author. Deleting a referenced author is restricted, preventing accidental removal
of its books. Deleting a book leaves its author intact. Names and titles are not
unique: matching names must not silently merge people, and different books may
share a title. Identity keys, deletion behavior, and the normalized relational
schema are implementation choices, not extra PDF requirements. The database
column spelling does not alter the existing JSON `subTitle` contract.

Request setters already trim names/titles and validate their 3-100 character
length. The database additionally rejects null/overlong values and values shorter
than three characters after trimming ordinary SQL spaces. These checks protect
storage; the request layer remains responsible for full .NET whitespace trimming
and useful validation messages before the book service performs a write.

### Development sample data and repeat startup

The seeder is registered **only when the environment is Development**. When both
`Authors` and `Books` are empty, it adds three authors and five books in one save:
Frank Herbert (*Dune*, *Dune Messiah*), Ursula K. Le Guin (*The Left Hand of
Darkness*, *The Dispossessed*), and Jane Austen (*Pride and Prejudice*).
All IDs come from SQL Server; one book has a subtitle and the others have null.

If either table has a row, the entire seed is skipped. This deliberately bootstraps
an empty catalog: it does not top up a partial catalog, match authors by name,
recreate a deleted sample book, or reset edited values. EF's migration lock covers
the empty check and save, preventing concurrent initializers from duplicating
the sample set. Runtime startup uses async operations and cancellation tokens;
the matching synchronous hook exists because EF's command-line tooling uses it.
See [EF Core seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding).
The migration contains schema only, with no `HasData` or sample-row inserts.

To inspect the data, connect in SSMS to `tcp:127.0.0.1,14333` with SQL authentication,
the local `sa` password, and **Trust server certificate**, then select `Bookstore`:

```sql
SELECT COUNT(*) AS AuthorCount FROM dbo.Authors;
SELECT COUNT(*) AS BookCount FROM dbo.Books;
SELECT b.BookId, b.Title, b.SubTitle, a.AuthorId, a.Name
FROM dbo.Books AS b
JOIN dbo.Authors AS a ON a.AuthorId = b.AuthorId
ORDER BY b.BookId;
SELECT MigrationId FROM dbo.__EFMigrationsHistory;
```

For a fresh catalog, expect 3 authors, 5 books, and one migration. Stop and start
the API again and rerun the queries: counts, IDs, and values should stay the same.
Stop the API, run `docker compose restart sqlserver`, wait for
`docker compose up -d --wait sqlserver`, and query again **before starting the API**
to show that the rows survived storage restart rather than being recreated.
Start the API again and confirm the same rows remain.

`bookstore_sqlserver-data` is the named volume mounted at `/var/opt/mssql`.
`docker compose stop sqlserver` and normal container restarts preserve it.
Keep the volume; removing it deletes database files. Changing the password in
`.env` does not change an existing database's `sa` password.

### Controlled production provisioning

Outside Development, startup neither migrates the schema nor registers demo
seeding. Provision SQL Server and a database through a controlled deployment,
back up existing data, and review an idempotent migration script:

```powershell
dotnet ef migrations script --idempotent --project src/Bookstore.Api --output .artifacts/bookstore-migrations.sql -- --environment Production
```

Ensure `.artifacts` exists first and supply `ConnectionStrings__Bookstore` through
the deployment's secret configuration. Apply the reviewed SQL with a deployment
identity allowed to change the schema; configure the runtime API with a different
login limited to the required data operations. Use a trusted SQL Server certificate
and `TrustServerCertificate=False`. Also set `Authentication__Authority` to the
deployed HTTPS issuer (including its trailing slash) and `Authentication__Audience`
to `bookstore-api`. The base Authority placeholder is intentionally empty, so a
Production host cannot silently trust the local development issuer. This local Developer-edition Compose service
is a demo setup, not production provisioning. Never run a production migration
with the Development environment selected.

### Persistence verification performed

Checked locally on Windows against the Compose SQL Server container:

- Applied the initial migration in Production and confirmed both tables remained
  empty. Production API startup also left them empty.
- Started the API in Development and found 3 authors and 5 books. A second startup
  and a Development `dotnet ef database update` preserved all IDs and field values.
- Edited a sample title, restarted the API, and confirmed the edit was preserved
  without another copy being added. Restored that temporary verification edit.
- Restarted SQL Server normally and compared every row **before** restarting the
  API. All data survived. The next API startup using user secrets preserved it too.
- Verified required fields, 3/100-character boundaries, rejected overlong values,
  generated IDs, a shared author, foreign-key enforcement, restricted author
  deletion, optional/long subtitles, and author preservation after book deletion.
  These SQL constraint checks ran in transactions that were rolled back.
- Confirmed no pending EF model changes and generated the production idempotent
  SQL script. The production script was generated, not deployed to a production
  environment.

Those checks describe the persistence checkpoint. Subsequent API security and
container checks are in the [security](docs/api-security-swagger.md#verification)
and [Docker](docs/docker-demo.md#verification) guides. Visual Studio UI startup
remains unverified. No integration-test project or database-test package was introduced.

## Contract

### Official assignment requirements

Both pages of the original local `docs/assignment.pdf` have now been read.
The assignment calls for a .NET bookstore backend with:

- A Book containing required integer (`int32`) `bookId`, nested `author`, and
  `title` of 3-100 characters; `subTitle` is an optional string with no specified
  length limit. An Author requires integer (`int32`) `authorId` and `name` of
  3-100 characters.
- Book CRUD protected by OAuth2 client credentials, and title/author search with
  pagination protected by OAuth2 implicit flow.
- A usable console/simple web test client or an appropriately configured Swagger
  page for exercising the API.
- An in-memory database or SQL Server Express/Developer; code-first is allowed.
  Durable SQL Server persistence is our choice within those permitted options.
- Docker support and minimal preparation to run in Visual Studio or VS Code,
  assuming Docker Desktop is installed.

The assignment permits third-party OAuth providers and gives Duende IdentityServer
as an example; it does not mandate that provider. It asks for production readiness
and appropriate best practices, with a three-day delivery window.

### Agreed implementation decisions and assumptions

Our choices are stable .NET 10, ASP.NET Core controllers, the three-project
solution above, EF Core with SQL Server Developer in Docker, and a local
OpenIddict server with minimal ASP.NET Core Identity. The PDF does not prescribe
those versions, libraries, or project boundaries. xUnit tests, setup documentation,
and the CI workflow are our agreed delivery practices.

The following contract details are agreed assumptions beyond the PDF's model and
flow requirements. Contracts, validation, persistence, book CRUD, and search are
implemented.

- Book and Author IDs are database-generated integers. Book write DTOs omit
  `bookId`; responses include both IDs. Explicit `bookId` on creation is rejected;
  callers cannot select new primary keys.
- An author without `authorId` creates a new author. A positive `authorId` must
  exist (otherwise 400), and the supplied name must match case-sensitively after trimming
  (otherwise 409). Book writes do not rename shared authors or merge them by name.
- Trim titles and author names before applying the assignment's length limits.
- CRUD uses `/api/books`; search uses `/api/books/search`. PUT replaces editable
  fields using the route ID, does not upsert, and clears a missing/null subtitle.
- POST returns 201 with the representation and Location; GET/PUT return 200;
  DELETE returns 204. Missing books return 404 and invalid requests return 400.
  Errors use consistent ProblemDetails responses.
- Search parameters are `title`, `author` (name), `pageNumber`, and `pageSize`.
  Matching uses case-insensitive substrings and AND between supplied filters.
  Trimmed blank filters are absent; no filters means all books, paginated.
- Default page number is 1 and page size is 10; maximum page size is 100.
  Invalid pagination returns 400. Order by `bookId`, include `totalCount` before
  pagination, and return 200 with empty items for no matches or pages beyond the end.
- No separate author API, registration UI, or unrelated features are planned.

Both real OAuth flows and their verification are documented in the Auth guide.
Token lifetime, audience, client IDs, implicit consent, and the development
callback are our implementation choices, not extra assignment requirements.
The required implicit flow is implemented explicitly; Authorization Code with
PKCE is a production recommendation. API enforcement, Swagger, Docker
demonstration, and CI are implemented. Local final verification is recorded;
Visual Studio UI and another Windows laptop still need manual verification.

### Current implementation

`Models/Book.cs` and `Models/Author.cs` are mapped by `Data/BookstoreDbContext.cs`
to SQL Server. DTOs in `Contracts/` describe the JSON
sent to and returned from the API, separately from the database models.

`BookWriteRequest` contains the editable fields for a replacement.
`CreateBookRequest` reuses those fields and rejects an explicitly supplied
`bookId`, even if its value is `0` or `null`. Unmapped JSON is captured only to
detect that field; unrelated unknown properties retain the serializer's normal
tolerance. `AdditionalProperties` is a JSON extension-data holder, not a named
field in the request contract.

Title and author-name setters trim input before the built-in required/length
validation runs. Request properties are nullable so missing input can produce
validation errors; this does not make required fields optional. An absent/null
`authorId` represents a new author; a supplied integer must be positive.
The book service checks an existing author's ID and name before writing.
Subtitle text is preserved, with no extra length limit. A missing or null subtitle
becomes `null` in the replacement request and clears the stored value on PUT.

`BookSearchRequest` trims title/author filters, treats blanks as absent, validates
pagination, and calculates the offset. `BookService` executes the filtered query.
`BookResponse` includes both IDs with a nested author; `BookSearchResponse` adds `items`, `totalCount`,
`pageNumber`, and `pageSize`.

### Contract examples

Example payload for `POST /api/books`:

```json
{
  "title": "Dune",
  "author": { "name": "Frank Herbert" },
  "subTitle": "An illustrated edition"
}
```

Created response shape; IDs here are illustrative database-generated values:

```json
{
  "bookId": 1,
  "author": { "authorId": 1, "name": "Frank Herbert" },
  "title": "Dune",
  "subTitle": "An illustrated edition"
}
```

To reference an existing author, the write payload uses
`"author": { "authorId": 1, "name": "Frank Herbert" }`. The service checks
that ID exists and the trimmed name matches. Search accepts
`title=Dune&author=Herbert&pageNumber=1&pageSize=10`. For a query with no matches,
the response is:

```json
{
  "items": [],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 10
}
```

These examples are included in OpenAPI. Unit tests cover input/author rules,
token and scope validation, and exception response behavior. SQL Server, live
HTTP, both real OAuth flows, and browser checks are recorded separately in the
[final verification record](docs/final-verification.md).

## Local files

Shared ignore rules cover IDE/build output, local SDK/package caches, environment
files, and private key files. Track only placeholder environment examples; use
user secrets or ignored local environment files for credentials.
The existing local exclusions for `AGENTS.md` and `docs/assignment.pdf` are
preserved in `.git/info/exclude`; neither file belongs in Git.

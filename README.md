# Bookstore API

An ASP.NET Core bookstore API. The solution setup, Book/Author contracts and
validation, unit tests, EF Core SQL Server persistence, and book CRUD are implemented.
SQL Server runs in Docker; the applications still run locally or in Visual Studio.
Search, authentication, a demonstration client, application containers,
and CI are planned for later checkpoints.

## Solution

Open `Bookstore.slnx`, the solution containing all three .NET 10 projects:

| Project | Purpose |
| --- | --- |
| `src/Bookstore.Api` | Book CRUD controller/service, contracts, ProblemDetails errors, EF Core DbContext, migrations, and development sample data; search comes next. |
| `src/Bookstore.Auth` | Empty web host; future local OAuth2 server using OpenIddict and minimal ASP.NET Core Identity. |
| `tests/Bookstore.UnitTests` | xUnit tests for validation, JSON contracts, author-reference rules, error responses, and safe exception logging. |

## Prerequisites and Visual Studio

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

## Build, test, and run

From the repository root in PowerShell:

```powershell
dotnet --version
dotnet restore Bookstore.slnx
dotnet build Bookstore.slnx --no-restore
dotnet test tests/Bookstore.UnitTests/Bookstore.UnitTests.csproj --no-build --no-restore
```

The first command should report `10.0.400` or a newer stable `10.0.4xx` patch.
Restore downloads EF Core and the test packages from NuGet; no database is needed to build or
run these unit tests. The validation tests call MVC's object validator directly,
including its validation of nested authors. JSON tests use the web serializer
defaults. No HTTP server, database, or integration-test packages are involved.

Verified for the CRUD step with SDK `10.0.400`: package restore passed,
build passed with zero warnings/errors, and all 66 unit-test cases passed.
Database checks are recorded below; the unit tests themselves use no database.

Complete the database setup below before starting the API. To start either host
(use separate terminals for both):

```powershell
dotnet run --project src/Bookstore.Api --launch-profile http
dotnet run --project src/Bookstore.Auth --launch-profile http
```

The API uses `http://localhost:5100`; Auth uses `http://localhost:5200`.
The API serves the CRUD routes below; its root path still returns 404.
Auth remains a placeholder host with no endpoints.
In Development, API startup applies migrations and initializes an empty catalog.
In Visual Studio, set either web project as the startup project and select its
`http` profile. For the optional `https` profiles, first trust the local development
certificate with `dotnet dev-certs https --trust`; HTTPS uses ports 7100 and 7200
respectively. Certificate trust and HTTPS startup have not been verified here.

## Book CRUD

This is a local development checkpoint. The endpoints currently have no
authentication; the required OAuth client-credentials protection is a later step.
Search and its pagination are also a later step.

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
into JSON or producing reference cycles. The schema is unchanged in this step.

### Try the endpoints from PowerShell

Complete the [local database setup](#local-setup), start the API with the run
command above, then run in another terminal:

```powershell
$booksUrl = 'http://localhost:5100/api/books'
$creation = @{
    title = '  Demo Book  '
    author = @{ name = 'Demo Author' }
    subTitle = 'First edition'
} | ConvertTo-Json

$created = Invoke-RestMethod -Method Post -Uri $booksUrl -ContentType 'application/json' -Body $creation
$bookUrl = "$booksUrl/$($created.bookId)"
Invoke-RestMethod -Uri $bookUrl

$replacement = @{
    title = 'Updated Demo Book'
    author = @{ authorId = $created.author.authorId; name = $created.author.name }
} | ConvertTo-Json

# Omitting subTitle clears it. The book ID and author ID stay the same here.
Invoke-RestMethod -Method Put -Uri $bookUrl -ContentType 'application/json' -Body $replacement
Invoke-WebRequest -UseBasicParsing -Method Delete -Uri $bookUrl | Select-Object StatusCode
```

This walkthrough creates one author. Deleting the demo book intentionally leaves
that author in the database. Use IDs returned by the API rather than assuming a
particular seed ID.

### Errors and checks

Errors use `application/problem+json`, including a status, title, type, request
path in `instance`, and a `traceId`. Validation failures also include an `errors`
dictionary; an unknown author is reported under `Author.AuthorId`. Invalid JSON,
missing required fields, and failed length checks return 400. Unsupported media
types return 415; unsupported methods return 405. Unknown routes return 404.

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
project or additional test package was added. OAuth/401/403, search, HTTPS, and
Visual Studio UI behavior were not verified in this checkpoint.

## SQL Server persistence

### Local setup

Use Docker Desktop with Linux containers. Compose runs SQL Server 2022 Developer
on `127.0.0.1,14333` by default, bound only to the local machine. The API connects
to the `Bookstore` database. A separate authentication database will be added to
this same server in the authentication step.

1. Copy the placeholder file once: `Copy-Item .env.example .env`. If `.env`
   already exists, preserve it. Set `MSSQL_SA_PASSWORD` to a strong local password
   (at least 8 characters, including upper/lowercase letters, digits, and a symbol).
   Single-quote the value in `.env` if it contains `$` or `#`, so Compose treats
   those characters literally. `.env` is ignored; only `.env.example` is tracked.
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
dotnet run --project src/Bookstore.Api --launch-profile http
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
and `TrustServerCertificate=False`. This local Developer-edition Compose service
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

Visual Studio UI startup and HTTPS were not exercised. Full application containers,
search, and OAuth remain later checkpoints. No integration-test
project or database-test package was introduced.

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
and the later CI workflow are our agreed delivery practices.

The following contract details are agreed assumptions beyond the PDF's model and
flow requirements. Contracts, validation, persistence, and book CRUD are
implemented. Search request validation exists; search execution is the next step.

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

Later steps will document both real OAuth flows, application containers, and
their verification as they are implemented. Implicit
flow is required for this assignment; it is a legacy flow, and Authorization Code
with PKCE is a planned production recommendation rather than a replacement here.

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

`BookSearchRequest` trims title/author filters, treats blanks as absent, and
validates pagination. It does not execute a search. `BookResponse` includes both
IDs with a nested author; `BookSearchResponse` adds `items`, `totalCount`,
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
that ID exists and the trimmed name matches. The future search endpoint will accept
`title=Dune&author=Herbert&pageNumber=1&pageSize=10`; its response shape is:

```json
{
  "items": [],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 10
}
```

These examples will also be included in OpenAPI when the demonstration client is
added. Unit tests cover input/author rules and exception response behavior; the
SQL Server and live HTTP checks are separate manual verification. OAuth has not
been implemented or verified yet.

## Local files

Shared ignore rules cover IDE/build output, local SDK/package caches, environment
files, and private key files. Track only placeholder environment examples; use
user secrets or ignored local environment files for credentials.
The existing local exclusions for `AGENTS.md` and `docs/assignment.pdf` are
preserved in `.git/info/exclude`; neither file belongs in Git.

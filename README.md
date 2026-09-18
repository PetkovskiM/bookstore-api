# Bookstore API

An ASP.NET Core bookstore API. The solution setup, Book/Author models, request
and response contracts, input validation, and focused unit tests are implemented.
Database access, authentication, book endpoints, a demonstration client, Docker,
CI, and integration tests are planned in the [ordered branch plan](docs/implementation-plan.md).

## Solution

Open `Bookstore.slnx`, the solution containing all three .NET 10 projects:

| Project | Purpose |
| --- | --- |
| `src/Bookstore.Api` | Web host with controller support, Book/Author models, and validated request/response contracts; future CRUD, search, and persistence. |
| `src/Bookstore.Auth` | Empty web host; future local OAuth2 server using OpenIddict and minimal ASP.NET Core Identity. |
| `tests/Bookstore.UnitTests` | xUnit tests for request validation, normalization, pagination boundaries, and JSON contracts. |

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
Restore downloads the test packages from NuGet; no database is needed to build or
run these unit tests. The validation tests call MVC's object validator directly,
including its validation of nested authors. JSON tests use the web serializer
defaults. No HTTP server, database, or integration-test packages are involved.

Verified for the contracts/validation step with SDK `10.0.400`: restore passed,
build passed with zero warnings/errors, and all 53 unit-test cases passed.

To start an empty host, run one of these commands (use separate terminals for both):

```powershell
dotnet run --project src/Bookstore.Api --launch-profile http
dotnet run --project src/Bookstore.Auth --launch-profile http
```

The API uses `http://localhost:5100`; Auth uses `http://localhost:5200`.
An HTTP 404 is expected for every path because neither host has endpoints yet.
In Visual Studio, set either web project as the startup project and select its
`http` profile. For the optional `https` profiles, first trust the local development
certificate with `dotnet dev-certs https --trust`; HTTPS uses ports 7100 and 7200
respectively. Certificate trust and HTTPS startup have not been verified here.

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
flow requirements. Request shapes and local validation are implemented; database
behavior, HTTP responses, and actual search results await later steps.

- Book and Author IDs are database-generated integers. Book write DTOs omit
  `bookId`; responses include both IDs. Explicit `bookId` on creation is rejected;
  callers cannot select new primary keys.
- An author without `authorId` creates a new author. A positive `authorId` must
  exist (otherwise 400), and the supplied name must match after trimming
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

Later steps will document SQL Server/migrations, development data, local secrets,
both real OAuth flows, Docker, and verification as they are implemented. Implicit
flow is required for this assignment; it is a legacy flow, and Authorization Code
with PKCE is a planned production recommendation rather than a replacement here.

### Current implementation

`Models/Book.cs` and `Models/Author.cs` hold the data that will later be mapped by
EF Core. They do not connect to a database. DTOs in `Contracts/` describe the JSON
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
`authorId` represents a new author; a supplied integer must be positive. Verifying
an existing author's ID and name needs the future database service.
Subtitle text is preserved, with no extra length limit. A missing or null subtitle
becomes `null` in the replacement request; clearing the stored value comes later.

`BookSearchRequest` trims title/author filters, treats blanks as absent, and
validates pagination. It does not execute a search. `BookResponse` includes both
IDs with a nested author; `BookSearchResponse` adds `items`, `totalCount`,
`pageNumber`, and `pageSize`.

### Contract examples

Example creation payload (the endpoint is not implemented yet):

```json
{
  "title": "Dune",
  "author": { "name": "Frank Herbert" },
  "subTitle": "An illustrated edition"
}
```

Expected response shape once creation and persistence are implemented; IDs here
are illustrative database-generated values:

```json
{
  "bookId": 1,
  "author": { "authorId": 1, "name": "Frank Herbert" },
  "title": "Dune",
  "subTitle": "An illustrated edition"
}
```

To reference an existing author, the write payload uses
`"author": { "authorId": 1, "name": "Frank Herbert" }`. The service will check
that ID exists and the trimmed name matches. A search request can supply
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
added. No HTTP status codes, persistence, or OAuth behavior are proven by the
current unit tests.

## Local files

Shared ignore rules cover IDE/build output, local SDK/package caches, environment
files, and private key files. Track only placeholder environment examples; use
user secrets or ignored local environment files for credentials.
The existing local exclusions for `AGENTS.md` and `docs/assignment.pdf` are
preserved in `.git/info/exclude`; neither file belongs in Git.

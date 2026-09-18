# Bookstore API

An ASP.NET Core bookstore API. This first step contains
only the solution and project setup. Database access, authentication, book
endpoints, a demonstration client, Docker, CI, and integration tests are not
implemented yet. There are no template endpoints or placeholder tests.

## Solution

Open `Bookstore.slnx`, the solution containing all three .NET 10 projects:

| Project | Purpose |
| --- | --- |
| `src/Bookstore.Api` | Web host with controller support; future book CRUD, search, and persistence. |
| `src/Bookstore.Auth` | Empty web host; future local OAuth2 server using OpenIddict and minimal ASP.NET Core Identity. |
| `tests/Bookstore.UnitTests` | xUnit project referencing both hosts; future tests for validation and application rules. No tests exist yet. |

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

## Build and run

From the repository root in PowerShell:

```powershell
dotnet --version
dotnet restore Bookstore.slnx
dotnet build Bookstore.slnx --no-restore
```

The first command should report `10.0.400` or a newer stable `10.0.4xx` patch.
Restore downloads the test packages from NuGet; no database is needed to build.
Building the test project does not mean tests have passed: there are no tests
to run in this step. Useful xUnit tests will accompany application rules later.

Verified during setup: solution restore and build passed with SDK `10.0.400`,
with zero build warnings or errors. No test run was performed.

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

## Planned contract

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
flow requirements. They describe future behavior; none is implemented yet.

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

## Local files

Shared ignore rules cover IDE/build output, local SDK/package caches, environment
files, and private key files. Track only placeholder environment examples; use
user secrets or ignored local environment files for credentials.
The existing local exclusions for `AGENTS.md` and `docs/assignment.pdf` are
preserved in `.git/info/exclude`; neither file belongs in Git.

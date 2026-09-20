# Implementation plan

Plan for **10 working branches in total**, excluding `main`: setup through CI is
merged, and `docs/final-verification` is the current branch. The developer confirmed
both hosted CI runs passed after merging the CI checkpoint.
The developer creates each branch from updated `main` after merging the previous
PR and handles all staging, commits, pushes, and merges.

Each branch is one complete reviewable step. Useful unit tests and README updates
accompany the feature that needs them. This plan implements the agreed stack and
contract; it does not turn those choices into extra assignment requirements.

| Order | Branch | Scope and completion check |
| --- | --- | --- |
| 1 | `chore/project-setup` | Merged: one solution, three projects, stable SDK selection, formatting/ignore rules, and setup instructions; restore/build verified. |
| 2 | `feat/book-contracts-validation_` | Merged: Book/Author models, request/response DTOs, trimming and validation, pagination defaults, JSON contracts, and meaningful unit tests. |
| 3 | `feat/book-persistence` | Merged: Bookstore DbContext and SQL Server EF Core migrations; one SQL Server Developer container with a health check and persistent named volume, local secrets/placeholders, and idempotent Development-only book/author data. Verify migrations and data survive a container restart. Applications still run in Visual Studio. |
| 4 | `feat/book-crud` | Small async book service and CRUD controllers with cancellation tokens, generated IDs, existing-author rules, replacement behavior, and correct responses. Add built-in ProblemDetails, exception handling, and safe logging. Verify against SQL Server and unit-test application rules. |
| 5 | `feat/book-search` | Title/author substring search with case-insensitive AND filters, stable ordering, counts, pagination, and empty results. Verify SQL Server case handling and pagination boundaries; add focused rule tests. |
| 6 | `feat/oauth-server` | Local OpenIddict server and minimal Identity login/logout, separate authentication database in the existing SQL Server container, migrations, and idempotent Development-only demo user/clients. Restrict grants/scopes; verify real client-credentials and implicit token issuance, HTTPS, discovery, and callbacks. |
| 7 | `feat/api-security-swagger` | Validate issuer, audience, signature, and expiry; enforce `books.manage` on CRUD and `books.search` on search. Add configured Swagger, contract examples, and a management-token demonstration. Verify both real flows and 401/403 behavior, including tokens used for the wrong operation. |
| 8 | `feat/docker-demo` | Containerize API/Auth and complete Compose with the existing SQL Server service and two databases. Document certificates, secrets, migrations, hostname/issuer configuration, and health checks. Verify CRUD/search, persistence, discovery, redirects, and both OAuth flows in the full Docker run mode. |
| 9 | `ci/build-and-tests` | Merged: GitHub Actions restores, builds in Release, and runs unit tests on pushes to and pull requests targeting `main`, with a workflow badge. It uses `global.json` and needs no SQL Server service. Both hosted runs passed, as confirmed by the developer. |
| 10 | `docs/final-verification` | Current: local build/tests/format, requirement review, Docker/live OAuth and restart persistence checks, Production guidance, and delivery instructions. The Docker initializer now has an SDK-free reviewer path; its fresh clean-laptop retest remains manual. See the [verification record](final-verification.md) and [clean-machine/demo guide](delivery-guide.md). Optional Visual Studio UI remains a separate manual check. |

Priority is a working, persistent book API, then the required OAuth behavior and
demonstration client, then the complete Docker run mode and delivery checks.
The CRUD/search branches are intermediate local development steps; assignment
completion requires the scope protection added in branch 7. CI is a short delivery
step; the build and existing tests still run locally on each preceding branch.

Production database provisioning and migration instructions are introduced with
persistence and extended with authentication/Docker. Migrations never contain demo
users, clients, or credentials. Development seeding must not reset existing data or
passwords, and verification must not delete database volumes.

## Optional final-stage integration tests

Only if explicitly chosen at the final stage, insert `test/integration-tests`
before final verification. That makes **11 working branches total**. Use
`WebApplicationFactory` and isolated SQLite in-memory data with an open connection
for the database lifetime. Do not use Testcontainers.

No integration-test project or packages are added in advance. These optional tests
cannot replace manual SQL Server migration/persistence checks or real OAuth flow
verification in either supported run mode. The assignment's three-day window makes
required functionality and reproducible delivery the priority.

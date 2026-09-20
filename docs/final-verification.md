# Final verification record

Reviewed on 2026-09-20 on the existing Windows development machine, on
`docs/final-verification`, starting from a clean working tree at the CI merge
`5329dfc`. Both pages of the local assignment PDF, project guidance, README,
and all existing documentation were read before changes.

**Pass** means checked in this review, with the evidence described below.
**Manual** means not established by this review. Earlier checkpoint results remain
in their original guides and are not presented as newly rerun tests.

Update after the first second-laptop attempt: the former Docker preparation required
SDK `10.0.400`, User Secrets, and `dotnet dev-certs`, so it did not meet the intended
minimum Docker-only reviewer path. The initializer and documentation now remove those
host dependencies. Its syntax and existing-configuration preservation were checked on
the development machine; a fresh Docker clean-laptop retest is still required.

## Requirement checklist

| Requirement | Status | Evidence and limits |
| --- | --- | --- |
| Book/Author contract | Pass | DTO/model/OpenAPI review and unit tests: nested author, required IDs in responses, trimmed 3-100-character title/name, exact `subTitle` spelling, no added subtitle length limit. Generated keys and write assumptions remain documented separately from the PDF. |
| CRUD via client credentials and `books.manage` | Pass | Real token issuance; POST 201 with generated book/author IDs and Location, GET/PUT 200, omitted subtitle cleared, DELETE 204, subsequent GET 404. Author reuse, unknown-author 400, conflicting-name 409, invalid writes and caller-supplied book ID 400 checked. |
| Implicit search and `books.search` | Pass | Real Identity login and Swagger callback; case-insensitive title/author substrings, AND filters, defaults, blank/no filters, count/order, two pages, no matches, beyond-last/large page and invalid pagination checked. |
| Issuer, audience, signature and expiry | Pass | Discovery and public-only JWKS, real RS256 `at+jwt` tokens with expected issuer/audience/scope and 15-minute lifetime. Existing cryptographic unit tests reject wrong issuer/audience/key, expiry/not-before failures, missing expiry, wrong type/algorithm and unsigned tokens. Those negative variants were unit-tested, not all issued by the live server. |
| Authentication and authorization errors | Pass | All five book operations returned 401 without a token. Malformed/tampered tokens returned 401. Search tokens returned 403 on every CRUD action; management tokens returned 403 on search. ProblemDetails and safe Bearer challenges checked. Rejected writes left existing rows unchanged. |
| OAuth restrictions and browser safety | Pass | Invalid management credentials, disallowed grants/scopes, browser token-endpoint use and unregistered callbacks rejected. Real popup callback, secure/HttpOnly/SameSite cookie, logout antiforgery, logout, standalone callback fragment cleanup and no browser token persistence checked. |
| Swagger/demo client | Pass | Both schemes and all five operations present. Actual search and management **Try it out** worked in headless Edge with normal HTTPS trust. Reload cleared Swagger authorization; no external validator requests occurred. |
| SQL persistence and existing migrations | Pass | Both migration-history entries present; both IDs are SQL identity columns. No pending EF model changes; both idempotent Production scripts generated in memory. Full catalog/user/client/migration snapshots survived normal SQL restart, compared before application startup. |
| Development seeding | Pass / Manual | Source gates seeding to Development; migrations contain no demo inserts. Repeated real startup preserved existing rows and user/client credentials. Fresh-database bootstrap was verified in earlier checkpoints; rerunning it on the second laptop remains manual. No existing database was reset here. |
| Full Docker demo | Pass / Manual | Earlier `up -d --build --wait` checks built both images and started SQL/Auth/API; health, issuer routing, both flows, persistence and retained keys/cookies were checked. The new SDK-free first-run initializer has syntax and repeat-preservation evidence only; fresh Docker clean-laptop retest is required. |
| CI restore/Release build/unit tests | Pass | Workflow path and badge match; SDK comes from `global.json`, triggers target `main`, no runtime services/secrets required. All equivalent local commands passed. Hosted PR and post-merge runs/green badge were confirmed by the developer; no GitHub setting was changed. |
| Tracked secrets and generated files | Pass | Tracked-file inventory and content scan found no local credentials, private-key/JWT patterns, certificates, `.env`, runtime directories or build artifacts. Placeholder examples are intentional. This is a current-tree/known-value check, not a comprehensive scan of all historical commits. |
| Production migration/seeding/certificate boundary | Pass | Source review: migration/seed hooks and demo UI are Development-only. In Production, Auth failed without an explicit signing certificate; API failed without an explicit HTTPS authority. EF generated schema-only SQL without a live database or signing credentials. |
| Production deployment | Manual | Deployment secrets, restricted database identities, real users/clients, HTTPS names, certificates, persistent encrypted Data Protection keys, backups/rotation and any reverse proxy need provisioning and end-to-end testing. See the [Production checklist](delivery-guide.md#production-checklist). This is not a tested production installation. |
| Docker clean laptop / optional Visual Studio | Manual | The first Docker clean-laptop attempt exposed the former host SDK/User Secrets/development-certificate dependency. Retest the revised Docker-only checklist; optional Visual Studio UI startup/debugging remains separate and untested. |

## Commands and results

Commands ran from the repository root unless stated otherwise. Local Git inspection
used the per-command prefix `git -c safe.directory=D:/GIT/bookstore-api`; no global
Git configuration, staging, commits, branches, remotes, or GitHub settings were changed.
Expanded Compose output was deliberately suppressed because it contains the SQL
password. `config --quiet` performs the requested validation without disclosing it.

| Command | Result |
| --- | --- |
| `dotnet --version` | `10.0.400`, matching `global.json`. |
| `dotnet --list-sdks` | Installed SDK inventory included `10.0.400`. |
| `dotnet restore Bookstore.slnx` | Passed; all projects up to date. NuGet access used the approved normal-user execution context. |
| `dotnet build Bookstore.slnx --configuration Release --no-restore` | Passed before and after the test whitespace correction; zero warnings/errors. |
| `dotnet test tests/Bookstore.UnitTests/Bookstore.UnitTests.csproj --configuration Release --no-build --no-restore` | Passed before and after formatting: 140 passed, zero failed/skipped. |
| `dotnet format Bookstore.slnx --verify-no-changes --no-restore` | Initially found whitespace issues in two test files. Passed after the correction. |
| `dotnet format whitespace Bookstore.slnx --no-restore --include tests/Bookstore.UnitTests/Authentication/ApiTokenValidationTests.cs tests/Bookstore.UnitTests/Authentication/SwaggerContractTests.cs` | Applied only initializer line breaks and nested-loop indentation in those files. Test behavior unchanged. |
| `dotnet tool restore` | Initial sandbox attempt could not reach NuGet; approved retry restored existing `dotnet-ef` 10.0.12. No project package was added. |
| `dotnet ef --version` | `10.0.12` in the normal-user context. The isolated sandbox context could not find that user's restored tool. |
| `docker compose config --quiet` | Passed. |
| `docker compose --profile demo config --quiet` | Passed, including both applications. |
| `docker compose --profile demo ps` | Initial state: only SQL running, healthy. Docker access initially required execution outside the sandbox. |
| `docker compose --profile demo up -d --build --wait` | Passed; both application images built, all three services became healthy. Existing runtime files were used without regeneration. |
| `docker compose --profile demo exec -T api id -u` and the same command for `auth` | Both returned nonzero user IDs. |
| `docker compose --profile demo stop api auth` | Passed before persistence checks and at the end, restoring the original SQL-only running state. |
| `docker compose --profile demo restart sqlserver` | Normal restart passed. No volume was deleted/recreated. |
| `docker compose up -d --wait sqlserver` | Passed; verifier separately waited for user-database recovery before querying snapshots. |
| `docker compose --profile demo up -d --no-build --wait` | Passed after SQL restart; saved rows, keys, both issued tokens and the Identity cookie survived. |
| `docker inspect --format '{{json .Mounts}}' bookstore-sqlserver-1` | Captured privately before/after restart; the SQL volume mount matched. |
| `docker compose --profile demo logs --no-color api auth` | Captured in memory, not printed. No actual demo/PFX credentials or issued JWTs found. |
| PowerShell parser for `scripts/Initialize-DockerDemo.ps1` | Passed. The revised initializer has valid Windows PowerShell syntax and contains no `dotnet` command. |
| `scripts/Initialize-DockerDemo.ps1` against the existing complete runtime configuration | Passed. It reported valid preserved configuration; before/after SHA-256 comparisons of `.env`, both JSON files, public/private HTTPS files, and OAuth PFX files were all unchanged. No values or hashes were printed. |
| `git diff --check` | Passed after corrections/documentation updates. |

The following additional commands were executed for this review. The `.artifacts`
helpers are local verification tools, excluded from Git; they are not required
to build, run, or review a clean clone. Their checks are described here and in the
delivery guide, not dependent on distributing another machine's local files.

| Command or inspection | Result |
| --- | --- |
| `node .artifacts/read-assignment.cjs` | Read the text of both PDF pages without changing/publishing the PDF. |
| `node --check .artifacts/final-verification.cjs` | Local verifier syntax passed. |
| `node --use-system-ca .artifacts/final-verification.cjs --preflight` | Final run passed: tracked-file scan, both SQL migration snapshots, identity columns, matching local/Docker credentials, and protected-file hashes. |
| `node --use-system-ca .artifacts/final-verification.cjs` | Final complete run passed: 45 counted API/health/discovery HTTP checks, plus token-endpoint and browser traffic; live OAuth, CRUD/search, restart, fixture cleanup and original-data comparisons. |
| `node --use-system-ca .artifacts/final-verification.cjs --presentation-check` | Passed: reviewer snippet returned 403 using the actual Swagger search token, both Swagger requests worked, the expanded known-secret scan passed, and data/file comparisons matched. |
| `node --check .artifacts/final-production-check.cjs` | Production verifier syntax passed. |
| `node .artifacts/final-production-check.cjs` | Passed in the normal-user context: both EF model/script checks and both Production fail-fast checks. The first sandbox attempt could not access the restored EF tool. |
| `node -e <Markdown-link-check code>` | Initial inline attempt failed because Windows PowerShell removed JavaScript argument quotes. Moved the check to the local script below; no project code was involved. |
| `node .artifacts/final-docs-check.cjs` | Passed: local links/heading anchors and whitespace in all seven Markdown documents, one workflow matching its badge, and only the intended changed paths. |
| Public documentation/workflow link status pass | Passed: all 19 external Markdown references returned HTTP 200. Localhost application URLs require the Docker demo and were not restarted for this documentation check. |
| `dotnet ef migrations has-pending-model-changes --project src/Bookstore.Api --configuration Release --no-build -- --environment Production` | Passed inside the Production verifier with a dummy, unused database connection; no pending model changes. |
| Same EF model-check command with `src/Bookstore.Auth` | Passed. |
| `dotnet ef migrations script --idempotent --project src/Bookstore.Api --configuration Release --no-build -- --environment Production` | Passed; SQL held in memory, inspected for schema and absence of demo inserts, then discarded. No migration applied. |
| Same EF script command with `src/Bookstore.Auth` | Passed with the same constraints. |
| `dotnet <absolute-path>/src/Bookstore.Auth/bin/Release/net10.0/Bookstore.Auth.dll --urls http://127.0.0.1:0` | Expected startup failure in Production with an explicit test issuer but no signing certificate. No running issuer or new certificates resulted. Output captured privately. |
| Equivalent API DLL command, with Production and a blank authority | Expected startup failure requiring an explicit HTTPS authority. No API/database runtime started. |
| `vswhere.exe -all -prerelease -products '*' -requires Microsoft.VisualStudio.Workload.NetWeb -property installationVersion` | Found 17.8.34330.188 and 18.10.12106.202. Only the latter supports this .NET 10 setup; no IDE UI test performed. |
| `git status --short --branch`, `git branch --show-current`, `git diff --stat`, `git diff --cached --stat`, `git diff --numstat`, `git log -3 --oneline` | Confirmed branch, clean starting tree, CI merge base, and scoped unstaged changes. |
| `git ls-files`, `git ls-files -z`, `git diff --name-only`, `git check-ignore -v ...`, final `git diff` | Inspected tracked inputs, protected-file exclusions, and final changes without altering Git state. |
| `rg --files`, `rg -n ...`, `Get-Content`, `Get-ChildItem`, `Get-Command` | Read instructions/docs/source/tests/workflow and inspected available tools/file names; no private settings contents printed. |
| `Get-Process dotnet,node`, `Get-NetTCPConnection -LocalPort 7100,7200 -State Listen`, `Get-Date` | Checked running state/ports after the interruption and recorded the review date. No user process was terminated. |
| `node -e <read existing browser verifier package version>` | Confirmed the already-installed local browser verifier was available; no browser/test package was added to the project. |

The SQL verifier invoked `docker compose --profile demo exec -T sqlserver sh -c ...`
with `sqlcmd -S localhost -U sa -C -b -y 0 -d <database> -Q <query>`. The container's
existing SQL password was read inside the container, never placed in command
arguments or printed. Queries captured catalog rows, user/client records, migration
histories and SQL identity metadata in memory. They also removed only the exact
author/book IDs created by that verification run, guarded by a unique fixture name.

Two verification-tool issues were corrected locally: incompatible `sqlcmd`
header/output options, and querying a user database immediately after SQL's
master-database health check passed during recovery. The latter initially produced
a transient database-login failure; cleanup succeeded and the repeated check
passed after a bounded recovery wait. Neither required application/runtime changes.
The interrupted approval did not start the live workflow.

## Changed files

| File | Reason |
| --- | --- |
| `README.md` | Add the concise recommended Docker quick start and move SDK/Visual Studio/User Secrets material under the optional local path. |
| `docs/delivery-guide.md` | New architecture overview, Docker-only clean-laptop checklist, tested token/401/403 presentation sequence, and Production provisioning/proxy guidance. |
| `docs/final-verification.md` | New requirement checklist, command/results record, preservation evidence, limitations, and the required Docker clean-laptop retest status. |
| `docs/implementation-plan.md` | Mark CI merged with confirmed hosted results and record the SDK-free Docker retest as the remaining delivery check. |
| `docs/api-security-swagger.md` | Make Docker the primary Swagger flow and have its management-token command read ignored Docker `auth.json`. |
| `docs/docker-demo.md` | Remove host-SDK/User Secrets/development-certificate prerequisites; document generated runtime material, preservation, trust, and mode boundaries. |
| `docs/oauth-server.md` | Distinguish Docker credential storage from the optional local User Secrets path and retain Data Protection/rotation guidance. |
| `scripts/Initialize-DockerDemo.ps1` | Replace the User Secrets/`dotnet dev-certs` dependency with first-run ignored Docker configuration/certificate generation, repeat-run validation/preservation, optional current-user HTTPS trust, and no secret output. |
| `tests/Bookstore.UnitTests/Authentication/ApiTokenValidationTests.cs` | Formatter-only line breaks in three object initializers. |
| `tests/Bookstore.UnitTests/Authentication/SwaggerContractTests.cs` | Formatter-only indentation of a nested loop. |

Three new local-only helpers were also created:
`.artifacts/final-verification.cjs`, `.artifacts/final-production-check.cjs`, and
`.artifacts/final-docs-check.cjs`. They contain verification code, not credentials
or generated runtime configuration, and remain excluded from Git. Normal build
outputs remain ignored as well.

## Preservation and limits

All pre-existing authors/books, user credentials, client registrations and migration
records matched their initial snapshots after cleanup. Identity's concurrency stamp
may advance during a real successful login; token/authorization bookkeeping can
also grow normally. Temporary API inserts advance identity counters, so gaps in
generated IDs are expected. No counter was reseeded and no existing data was reset.

Hashes of `.env`, every existing `.local/docker` file, both user-secrets files,
`AGENTS.md`, `docs/assignment.pdf`, and `.git/info/exclude` were unchanged. Existing
volumes and certificates were preserved. Final running state was restored to SQL
only; start the full demo using the documented command when needed.

No application code, OAuth settings, migrations, runtime configuration, workflows,
certificates, secrets or seed data changed. The Docker initialization script and
documentation changed; the only C# edits are whitespace in two existing test files.
No integration-test project, package, framework or abstraction was introduced.

Ready for Docker clean-machine verification: **yes, as a retest candidate**. Follow
the [Docker-only checklist and demo sequence](delivery-guide.md) and record the
results. A successful first-run fresh-laptop result is still required before it can
be reported as passed. Optional Visual Studio startup/debugging, browser interaction
by a reviewer, non-Windows preparation, and a real Production deployment remain
manual. Historical local CLI/OAuth checks are in the
[security](api-security-swagger.md#verification) and [Auth](oauth-server.md#verification)
guides; this final live run exercised the full Docker mode.

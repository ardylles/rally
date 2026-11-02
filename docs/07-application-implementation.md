# First application implementation

The application now uses ASP.NET Core on .NET 10, React with TypeScript, and PostgreSQL. The original static prototype remains in `prototype/` as a design reference.

## Run locally on Windows

Prerequisites: .NET SDK 10.0.303 (or a compatible patch), Node.js 24, and PostgreSQL 17 binaries. Installed dependency versions are recorded in `web/package-lock.json` and the server package lock files.

From the repository root:

```powershell
powershell -NoProfile -File scripts/start-dev.ps1
```

Open [Rally](http://127.0.0.1:5080). Register your own account, create a club, then create a meet with your player names. No default account or shared password is included.

The script creates an isolated PostgreSQL cluster in `.local/postgres`, listening on 127.0.0.1:55432. It generates a development database password in the ignored `.local/db-password.txt` file. Existing PostgreSQL installations or databases are not modified. The application's cookie keys are stored in `.local/keys`.

Use `-PostgresBin` if the PostgreSQL binaries are installed elsewhere. Use `-SkipBuild` after a successful build. Stop the application with Ctrl+C; stop the project database with `scripts/stop-db.ps1` if it remains running.

If your network uses Windows-installed certificate authorities, Node can use that trust store without disabling TLS validation:

```powershell
$env:NODE_USE_SYSTEM_CA = '1'
npm.cmd ci --prefix web
```

## Working features

- Account registration, password hashing, cookie sign-in, and sign-out.
- Clubs with multiple organizers and registered club members.
- Meet-specific organizer assignments, enforced by the API.
- Persistent meet creation and editing before play.
- Manual named rosters with optional skills in skill-balanced mode.
- Fixed, rotating, skill-balanced, and mixed fixed/rotating scheduling.
- Fixed-pair editing before play or substitutions.
- Fairness display using planned and completed match counts.
- Match start controls with server timestamps and live player/court conflict checks within the meet.
- Game-level results for race to 4, race to 6, and best of 5 games.
- Optional winners/unforced errors with derived padel ratio and preserved null values.
- Match-only substitutions using any registered player outside the match, even if busy.
- New-player replacements from Players only, for the next match or remaining meet.
- Persisted match history, result corrections, and meet mutation snapshots.
- Optimistic revision checks to prevent lost updates.
- Responsive React interface served by ASP.NET Core in the built version.

To assign another organizer, that person first registers an account. A club organizer adds their registered email as a club member, then either promotes them to club organizer or assigns them to a specific meet. Adding a member does not send email. This first release treats roster players as meet participants; linking a participant to an account remains a separate pending capability.

## Architecture and persistence

The API lives in `server/Rally.Api`. The scheduler and meet rules are separate C# modules, independent of HTTP and PostgreSQL. The React client lives in `web`; all writes go through the API and all authorization is enforced server-side.

PostgreSQL stores users, clubs, memberships, meet organizers, meets, and audit events in relational tables. In this first implementation, each meet's roster, schedule, results, and statistics are a JSONB aggregate. Every accepted meet mutation locks its row, checks the expected revision, stores the previous aggregate as an audit snapshot, and commits the new revision atomically. This is an initial storage choice; extracting participant/match tables for cross-meet queries remains possible without changing the domain model.

Schema version 1 is defined in `Database.Schema` and recorded in `schema_versions`. Startup applies the idempotent initial schema. Future schema changes must use explicit numbered migrations rather than silently altering version 1.

The web build is emitted to the API's ignored `wwwroot`. Frontend requests use the same origin, and mutation requests require a custom header. Cookie sessions are HttpOnly and SameSite Lax. Development runs on localhost HTTP; production must use HTTPS and a protected persistent key store.

## Verification

```powershell
dotnet run --project server/Rally.Checks/Rally.Checks.csproj
npm.cmd --prefix web run build
node scripts/check-api.mjs
```

The last command requires the application running. It creates uniquely named test accounts, a test club, and meets in the project's development database, isolated from your own club. It writes a verification summary to `.local/verification.json`.

Verified:
- 44 C# domain checks: all partner modes, fixed-pair integrity, fairness, deterministic generation, substitutions, replacement scope, score validation, and optional statistics.
- 21 API/PostgreSQL checks: account access, club isolation, organizer scopes and revocation, multiple club organizers, stale/concurrent updates, results, substitutions, history, and audit entries.
- TypeScript checking and production frontend build.

The embedded browser blocked localhost with ERR_BLOCKED_BY_CLIENT, so visual and interactive browser verification is still pending. HTTP responses and database behavior were verified directly.

## Work still to implement

This is the first working application, not the complete production release.

- Reclub re-import reconciliation, persisted external profile provenance, and identity/account linking. Current import copies selected display names into a new meet draft.
- Player profile and history across multiple meets.
- General roster additions/removals, arrival windows and clock-time attendance. Round-based early departures and remaining-match regeneration are implemented.
- Cancelled/abandoned matches, paused timing, meet completion, and export.
- Shared physical court booking and registered-player conflict checks across meets.
- Multiple intervals per court, overnight sessions, automatic handling of overruns, and rescheduling after play begins.
- Mathematical optimality guarantees; the scheduler uses bounded search and can leave court slots idle to improve fairness.
- Invitations, email verification, password recovery, and role-change audit detail at club level.
- Automatic live refresh, offline use, accessibility/browser QA, backup restoration testing, and production deployment configuration.

Members currently see their club's meets; only club organizers and assigned meet organizers can mutate them. Club memberships are granted to already registered accounts. Full guest-to-account linking and granular sharing controls are not yet implemented.

## Verification update — 2 November 2026

The final backend solution build passed with zero warnings and errors. The React production build passed after formatting and read-only-dialog/configuration fixes. All 44 domain checks and 21 API/PostgreSQL checks passed again against the rebuilt server. The localhost application is ready for manual browser review; embedded-browser visual verification remains blocked.

## Court availability and Reclub update — 2 November 2026

Named court windows, planned round lengths, and Reclub roster preview are implemented. In Create meet (or Settings before play), set start 18:00, session 180 minutes, 3 courts, 9 rounds and 20 planned minutes per round. Enter:

```text
Court A | 18:00 | 21:00
Court B | 18:00 | 21:00
Court C | 18:00 | 20:00
```

Court C is eligible for rounds 1–6; Courts A and B through round 9. Every full match slot must fit inside the court booking. All times use the meet timezone. Leave availability blank for full-session availability on every court. Existing saved matches retain their history; regeneration adds planned times. Older meets without an explicit round length divide the session into whole-minute rounds.

Include changeovers in the planned round length. Actual play may overrun; timer buttons do not automatically reschedule matches. One interval per court and same-day sessions are supported.

Verification: 69 C# domain/parser checks, including the provided Reclub sample, and 26 API/PostgreSQL checks including a live Reclub fetch passed. TypeScript and production builds passed. Browser visual QA remains pending.

## Early-departure update

Players → Leave early records the first missed round and rebuilds remaining matches, preserving results, live matches and eligible substitutions. Schedule → Regenerate remaining can repeat the allocation using existing attendance settings. Fixed pairs with a missing player sit out until a remaining-meet replacement is added; mixed-mode partners join the rotating pool. See docs/02-scheduling-and-fairness.md for the full rules.

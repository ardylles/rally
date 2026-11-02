# Attendance, playing sides, public sharing and administration

## Player attendance

In Players, choose **Late / arrival** before the player starts playing. **Waiting for arrival** removes that player from unstarted assignments. When they arrive, select the current or a future round and save to rebuild the remaining schedule. Departure remains available separately.

Arrival cannot be moved before the current scheduling frontier or changed after that player has started or completed a match. Completed matches, live matches, scores, timers and statistics are preserved. Eligible explicit substitutions remain pinned; assignments that violate strict sides are rebuilt. Explicit registered substitutions are match-only attendance overrides.

Late players receive a fair share only from arrival onward. Scheduling replays prior rounds and gives a newcomer the active players’ average virtual game count at arrival. Missed rounds create no catch-up debt. Actual totals remain unchanged in history. Fairness is a bounded heuristic under court bookings, fixed pairs, sides and attendance, rather than a guarantee of equal totals across different attendance periods.

Fixed partners wait when either partner is unavailable. In mixed mode, an available player whose fixed partner is absent can join the rotating pool; their fixed pair resumes once both are available.

## Strict playing sides

Each player has Left, Right or Both; existing players default to Both. Left/Right are restrictions, not preferences. Valid teams are Left+Right, Left+Both, Right+Both and Both+Both. Left+Left and Right+Right are invalid.

Change a side in Players. Saving regenerates unstarted matches; a player in a live match must finish first. Fixed-pair side conflicts are rejected. Registered substitutions and new-player replacements must also form compatible teams. Newly arranged teams list the left-side player first; flexible players fill the required side.

The scheduler preserves compatible pairing capacity while favoring fair participation. Insufficient compatible players can leave courts unused; scheduling notes explain this. An odd total roster is allowed: spare players can rest.

## Public matches

Public view is off by default. A meet organizer can enable it on the meet dashboard, copy the public link, open the view, disable it or reset the link. Enabling works even after play starts.

The URL is `/public/{random-token}`. The API is `GET /api/public/{token}`. Tokens contain 256 bits of randomness and are stored separately from the meet document. Ordinary meet responses do not disclose the token. Only managers can retrieve sharing settings.

The public response explicitly includes meet display information, court bookings, player IDs/display names and match teams/status/times/scores. It excludes account links, emails, skills, statistics, substitutions, organizers and audit records. The page refreshes every 15 seconds. API responses use no-store caching.

Disabling returns 404 for the link; reenabling can restore that link. Reset creates a new token and permanently invalidates the previous URL. Deleting a meet also revokes access.

## Application administration

Application administrators are distinct from club and meet organizers. Administration lists users and meets and supports disabling, reenabling and deleting accounts, and deleting meets. Actions require confirmation in the UI and server-side admin authorization. The last active administrator cannot be disabled or deleted, including concurrent requests.

Disabled accounts cannot log in. Each authenticated request checks account existence, disabled state and session version. Disabling invalidates existing cookies; reenabling requires a fresh login. Deletion also invalidates access.

Account deletion detaches participant UserId links and removes club memberships and meet-organizer assignments. Participant names, match records, scores and statistics are retained unchanged. Historical actor display names remain in meet audit records. Administrative actions are recorded separately; meet deletion retains the administrative deletion record.

### Assign the initial administrator

Register the intended account first. For this installation the requested account is **ardylles@gmail.com**. In the deployed server environment, with the normal database connection configured, run:

```sh
dotnet Rally.Api.dll --bootstrap-admin ardylles@gmail.com
```

This explicit server-side provisioning command exits after updating the existing active account and recording the action. It does not create an account and never automatically grants admin privileges during signup. Run it once against each intended database. Local assignment does not change the Render/Neon account.

Do not put this command in the normal web start command: the web service should continue starting with `dotnet Rally.Api.dll` and its usual URL options.

## Migration and verification

Database migration 2 adds public tokens, admin/disabled flags, session versions, retained audit names and admin audit records. User foreign keys for ownership/audit permit deletion while retaining history. Initialization serializes migrations with a transaction advisory lock.

Domain checks cover all four meet modes, arrival fairness, preserved history, strict sides, substitutions, replacements, flexible-player capacity and restoration of empty schedules. API checks cover persistence, authorization, token reset/revocation and administration.

The optional administrator integration fixture is enabled through RALLY_TEST_ADMIN_DLL and requires the dedicated local database rally_features_20261102 and loopback URL. It creates disposable accounts and bootstraps only a test administrator. Production account credentials are not needed.

## Match substitutions and extra rounds

The match substitution picker includes every registered player outside that match, including busy, waiting, late and departed players. A manual registered substitution overrides attendance for that match only. It does not change arrival/departure dates or automatically add the player to other rounds. Regeneration retains the explicit substitution. Teams must still respect strict Left/Right restrictions, and starting a match still checks that players and the court are not already in a live match.

Use **Extra rounds** on the court overview or Schedule to append more rounds, even after the original schedule has finished. Existing matches, IDs, scores, statistics, timers and substitutions remain unchanged. New rounds use the current round length, previous participation, fixed partners, playing sides and attendance.

The form allows increasing the total session duration and extending individual court closing times. It never extends explicit bookings automatically; booking times are advisory and do not block new rounds. Courts without individual bookings continue using the full session window. Earlier court closing times remain respected.

Limits remain 20 total rounds, 480 session minutes, for the configured session (estimated matches may run later). Waiting players remain waiting when rounds are appended. An extension with no possible new match is rejected without changing the meet.

API: POST /api/meets/{id}/extra-rounds accepts revision, count, optional durationMinutes and optional courtAvailability (same courts, names and opening times; closing times may only extend). It requires meet-organizer access, checks revision conflicts and records an extra-rounds audit event.

See [flexible match management](12-flexible-match-management.md) for quick single-match generation, advisory booking times, restart/delete and confirmed full regeneration.

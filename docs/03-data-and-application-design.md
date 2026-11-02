# Data and application design

## Proposed architecture

Build a mobile-friendly ReactJS frontend, an ASP.NET Core API on .NET Core, and a PostgreSQL database. Keep the scheduling module isolated within the backend. Separate roster import from scheduling so a Reclub outage does not prevent a meet from running. The stack is confirmed; exact versions, hosting, data-access libraries, and authentication provider remain to be selected. See [Technical requirements](06-technical-requirements.md).

The scheduling module receives a validated snapshot and returns a candidate schedule plus diagnostics. It does not directly overwrite live matches. A server-side publish operation validates the latest meet state before committing a version.

## Core records

| Record | Important fields and purpose |
| --- | --- |
| User | Account identifier, login identity, display name; authentication is separate from Reclub. |
| Club | Stable club identifier, name, and settings; owns its meets. |
| ClubRole | Club, user, role, assigned by, timestamps, and revocation state; supports multiple club organizers with a unique active assignment per user/club/role. |
| Player | Stable internal person identifier; may exist without a user account. |
| UserPlayerLink | Verified account-to-player ownership and verification audit. |
| ExternalIdentity | Provider, stable external ID where available, public profile URL, source metadata, verification status. |
| IdentityClaim | Requesting user, claimed identity, method, review state, evidence reference, decision timestamps. |
| Meet | Club, name, creator, venue, timezone, window, state, mode, timing setting, revision. Creator is provenance, not the sole source of permission. |
| MeetRole | User, meet, role (including meet organizer), assigned by, timestamps, and revocation state; controls explicit access independently for each meet. |
| Court | Physical court identifier and venue; shared across meets when known. |
| MeetCourt | Meet allocation and availability intervals for a court. |
| MeetParticipant | Meet, player, name snapshot, skill snapshot, roster status, availability intervals. |
| FixedTeam | Stable meet team identity; effective membership history retained for whole-meet replacements. |
| ParticipantChange | Meet, optional fixed-team ID, outgoing/incoming participant IDs, scope (remaining meet or one match), entry source (match or players list), effective boundary or target match ID, state, organizer, reason, and timestamps. |
| ScoringRule | Format (race to 4, race to 6, best of 5 games), games target, maximum games where applicable, and display label. No point-by-point, deuce, or tiebreak-detail entry required. |
| ScheduleVersion | Meet, input snapshot, seed, algorithm version, fairness report, publication state. |
| Match | Stable ID, meet, court, round, expected interval, state, scoring snapshot, lock status, revision. |
| MatchParticipant | Match, actual participant, side A/B, fixed-team reference, and applicable replacement/substitution reference. |
| MatchResult | Scores, outcome type, winner if applicable, entered by, correction revision. |
| MatchPlayerStatistics | Match participant, nullable winners and unforced-error counts, entered/updated by, revision; padel ratio derived only when both counts exist. |
| MatchTiming | Start/end instants, recorded by, correction history; nullable when timing is disabled. |
| ScheduleAssignment | Version-to-match relationship preserving previous published schedules. |
| ImportBatch | Source URL, fetch time, parsed candidates, selected rows, import outcome. |
| AuditEvent | Actor, meet, action, timestamp, reason, previous/new values where appropriate. |

Guest identity merges must be reviewed and reversible. Preserve historical display snapshots while associating records with a verified player. Never merge people solely because names match.

Team membership changes are not player identity merges. Keep past match participants and their statistics unchanged when replacing a team member. A one-match substitute overrides only the target assignment; it does not alter ongoing team membership. Apply changes and affected future assignments in one transaction with conflict checks and version validation.

## State transitions

Meet: draft → scheduled → in progress → completed. Draft or scheduled meets can be cancelled. Cancelling an active meet preserves completed matches and explicitly resolves active and planned matches. Reopening a completed meet is an organizer action with an audit entry.

Match: planned → ready → in progress → completed. Planned or ready matches can be cancelled; in-progress matches can be abandoned. Readiness is derived from current availability and conflicts. With timing disabled, completion may be recorded without a start timestamp. Result correction creates a revision rather than silently replacing history.

Import: fetched → reviewed → committed, or failed/discarded. Fetching alone never changes the roster.

Identity claim: pending → verified, rejected, or withdrawn. Revocation is recorded and removes the account's derived access without erasing historical matches.

## Integrity and concurrent use

- Enforce meet-level authorization on every read and write; a guessed identifier must not expose another meet.
- Resolve organizer permissions from active club roles for the meet's club or explicit meet organizer assignments. A meet role must never grant club-wide access. Enforce role management on the server, audit changes, and prevent removal of the last club organizer transactionally.
- Keep user role assignments independent of player substitutions and roster membership. Replacing a player must not transfer organizer privileges to their substitute.
- Permit authorized skill edits with revision checks and retain previous schedule input snapshots. Hide skill fields when balancing is disabled, without erasing their stored values.
- Store times as UTC instants and display them in the meet timezone.
- Use transactions and version checks for publication, assignment edits, match start/end, and identity claims.
- Reject stale edits with a refresh-and-review response instead of silently overwriting another organizer's change.
- Repeated Start, End, or import confirmation requests must not create duplicate events or participants.
- Check shared court reservations and known player conflicts across meets. Unlinked guest identities cannot provide reliable cross-meet conflict detection; disclose this limitation.
- Prevent a participant from occupying both sides or appearing twice in a match.
- Preserve the distinction between scheduled history and actual played results.
- Store game totals without requiring small points, deuces, or tiebreak details. Validate normally completed outcomes against the format snapshot; distinguish partial abandoned results.
- Accept optional statistics as non-negative integers or null. Derive padel ratio as winners minus unforced errors only when both exist, and retain negative results.
- Attach statistics to actual match participants, including substitutes, and audit subsequent corrections. Statistics are optional independently of scores and timing.

## Timing behavior

Timing is optional per meet. Start records a server timestamp after checking current player and court availability. End records the finish timestamp and opens result entry; a missing score leaves a visible result-pending condition. Elapsed duration is derived from stored timestamps, so refreshing the screen does not reset a timer.

Timing corrections require an organizer and an audit reason. Ending a match cannot precede its start. Elapsed wall time includes any interruptions in the initial release; pause/resume is a later option.

## Suggested application areas

| Area | Purpose |
| --- | --- |
| Meets dashboard | Separate upcoming, active, and past meets; allow concurrent active sessions. |
| Meet setup | Roster, courts, availability, teams, scoring, and fairness settings. |
| Schedule preview | Assignments, rests, feasibility warnings, fairness report, and publication. |
| Live meet board | Current and next matches, start/end controls, game results, availability changes, and replacements/substitutes. |
| Meet history | Actual matches and players, game scores, optional statistics and padel ratio, durations, participation summary, and corrections. |
| Member profile | Personal history, account details, Reclub claim and verification state. |

## Operational requirements

Persist every accepted change before showing success. Surface connection failures and permit safe retries. Initial release assumes connectivity; offline editing and reconciliation require a separate design. Include database migrations, backups with a restore check, error logging without unnecessary personal data, and exportable meet history before production use.

## Substitution entry boundaries

Support all partner modes. Match-specific substitution accepts an existing MeetParticipant ID from the same meet and never creates a player. Validate current assignments and same-match uniqueness transactionally. Availability or assignments in other matches must not restrict substitute selection. Preserve those other assignments, while continuing to block simultaneous live use of the player or court at match start. Adding a new person is available only through the Players-list replacement operation, which registers the participant and updates the selected future assignments atomically. Neither operation transfers user roles or previous statistics.

## Mixed partner configuration

Add mixed to the meet partner mode. Persist selected fixed pairs as stable FixedTeam records; participants without a fixed-pair assignment belong to the rotating pool. Enforce at most one active fixed pair per participant per meet. Snapshot these preferences with each generated schedule. Display fixed partner names or rotating status in Players; mixed variety mode does not expose skill fields. Preserve the distinction between ongoing pair preferences and actual match participants after substitutions.

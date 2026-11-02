# Delivery plan and decisions

## Confirmed technical requirements

Use .NET Core with an ASP.NET Core API, ReactJS for the frontend, and PostgreSQL for persistent storage. These requirements were confirmed on 31 October 2026. See [Technical requirements](06-technical-requirements.md) for responsibilities and remaining implementation choices.

## Phase 1: Confirm the product rules

Review the requirement documents, confirm the remaining scheduling defaults and race-format winning-margin interpretation, and collect representative Reclub event/profile URLs. Match options are race to 4 or 6 games with tennis scoring, or best of 5 games; detailed point entry is unnecessary. Choose a skill scale and clarify whether equal opportunity or equal minutes is the primary expectation. Proposed default: equal match opportunities, with minutes reported when measured.

Exit criteria: agreed race-format winning-margin interpretation, initial operating mode, fairness priorities, and documented Reclub webpage extraction rules or a deferred scraping milestone.

## Phase 2: Build the scheduling foundation

Implement the scheduling module in .NET Core with fixed teams and rotating partners, feasibility diagnostics, deterministic generation, and fairness reports. Validate known feasible cases and hard constraints before connecting live meet operations.

Exit criteria: reproducible valid schedules for the scenarios in the fairness specification; explicit explanations when requests cannot be fulfilled.

## Phase 3: Deliver the organizer application

Support multiple club organizers and separately assigned meet organizers, including users who also play. Add organizer assignment management, meet-scoped authorization, editable skills visible only in balanced mode, and substitution entry directly from match views.

Build the ReactJS interface and ASP.NET Core API with PostgreSQL persistence. Implement organizer accounts, concurrent meets, manual rosters, courts, scoring settings, schedule preview/publication, availability changes, whole-meet replacements and next-match substitutes, game-result entry, optional player statistics and calculated padel ratio, optional timing, and persistent history. Include permissions and conflict checks from the first release.

Exit criteria: an organizer can run an entire multi-court meet, refresh without losing history, and revise future assignments after a departure. A second meet can run simultaneously without data interference.

## Phase 4: Add Reclub roster import

Implement Reclub webpage scraping, using browser rendering if required by the public page, plus preview and deduplication, controlled refresh, and useful failure behavior. Detect incomplete rosters and page changes. Keep manual and CSV entry available.

Exit criteria: supported URLs import reviewed rosters without duplicates; unsupported sources do not block meet creation.

## Phase 5: Add member accounts and identity claims

Implement member registration, verified player ownership, Reclub claims, controlled historical linking, and personal history. Include duplicate claims and unlinking behavior.

Exit criteria: verified members see their authorized history, and an unverified claimant cannot acquire another player's records or access.

## Phase 6: Improve from real sessions

Evaluate observed rest distribution, incomplete matches, partner coverage, skill balance, and organizer workload. Consider independent court progression, cross-meet variety, pause/resume timing, and offline support based on actual needs.

## Decisions to confirm

| Decision | Proposed starting point | Why it matters |
| --- | --- | --- |
| Initial platform | Mobile-friendly web application | Organizers and players can use phones without installing an app. |
| Fairness unit | Match opportunities; track completed matches and optional minutes separately | Match length varies, so equal matches cannot ensure equal court time. |
| Match options — confirmed | Race to 4 or 6 games with tennis scoring; best of 5 games (first to 3) | Record each team's games won. |
| Race winning margin | Working interpretation: reaching 4 or 6 ends the match, without a two-game margin | Still needs confirmation before strict score validation. |
| Detailed point entry — confirmed | No small-point, deuce, or tiebreak-detail entry required | Keep result entry at game-total level. |
| Optional statistics — confirmed | Winners, unforced errors, and padel ratio = winners minus unforced errors | Proposed entry granularity is each actual player per match; missing counts remain unknown. |
| Session planning | Session window plus estimated duration and optional target rounds | Allows feasibility estimates without promising exact finish times. |
| Court progression | Synchronized rounds initially | Makes rotations and rest easier to understand. |
| Skill balance | Optional organizer-entered scale; variety mode when unknown | Avoids treating missing ratings as beginner ratings. |
| Fixed-team absences — confirmed | Whole-meet replacement or a substitute for the next match only | Preserve the team identity and record actual players for each match. |
| History visibility | Participants and authorized organizers by default | Public display can be added explicitly. |
| Reclub access — confirmed | Scrape Reclub webpages supplied by URL; inspect representative pages to define extraction rules | Page structure and completeness still need validation. |
| Identity verification | Reviewed claims supported by scraped public profile data and ownership evidence | Public profiles alone cannot prove ownership. |

## Release review checklist

- All eleven original requirements map to PR-01 through PR-11; replacement and statistics additions map to PR-12 and PR-13.
- Live and historical match counts are distinct and understandable.
- Schedule changes preserve active matches and recorded history.
- Concurrency checks cover repeated requests and competing organizer edits.
- Account permissions and identity claim boundaries are exercised.
- Reclub failure leaves a usable manual workflow.
- Time estimates and infeasible coverage targets are clearly explained.
- Data persistence and backup restoration are verified.
- Replacement scope, automatic return after a one-match substitution, actual-player history, and substitute conflicts are verified.
- All three match formats work with game totals alone; abandoned results remain distinct.
- Optional statistics preserve blanks versus zeros, allow negative calculated padel ratios, and expose coverage in summaries.
- PR-14 through PR-16 cover conditional skill editing, multiple club organizers, and meet-specific organizer roles.
- Verify match-view substitution targets, hidden skills outside balance mode, retained skill edits, cross-meet role isolation, role revocation, and the last club organizer safeguard.

## Updated substitution acceptance criteria

- Substitution is available in fixed, rotating, and skill-balanced meets.
- A match accepts any player registered to that meet who is not already in the match, regardless of availability, and changes only that match.
- A new player can be registered as a replacement only from the Players list, with next-match or remaining-meet scope.
- Reject same-match duplicates, cross-meet participant IDs, and retroactive changes to started or completed matches. Permit overlapping planned assignments from manual substitution; retain live-match start conflict checks.

## Mixed-mode delivery addition

Include PR-17 in the scheduling foundation and organizer interface. Add fixed-pair selection within mixed meets, preserve pair integrity during generation, and report individual allocation across both groups. Verify equal counts in feasible ten-player/two-court/five-round examples with one or two fixed pairs, plus rest and capacity tradeoffs for odd rosters.

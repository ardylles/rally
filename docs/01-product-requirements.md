# Product requirements

## Purpose

Help an organizer turn a player list, court allocation, and session duration into a fair schedule, run the meet, and retain its history. Players should understand when they play, who they play with, and how many matches they receive.

## Roles

| Role | Capabilities |
| --- | --- |
| Club organizer | One or more users per club. Manage the club's meets, club organizer assignments, and meet organizer assignments, as well as rosters, courts, schedules, matches, results, and history. |
| Meet organizer | A user, including a participating player, assigned to a specific meet. Manage that meet's roster, courts, schedule, replacements, match operation, results, statistics, and history. No club-wide organizer access. |
| Member | Register, manage their own profile, request a Reclub identity link, and view authorized meet and personal history. |
| Guest player | Participate without registering; an organizer maintains their meet entry. |

Proposed default: meet information is accessible to authorized participants and organizers. Public sharing is an explicit organizer action with a limited display view.

A club can have multiple club organizers and a meet can have multiple meet organizers. Roles are additive: a player can also organize a meet. Proposed permission default: club organizers grant or revoke organizer assignments; meet organizers cannot promote themselves or others to club organizer or gain access to other meets. Keep at least one club organizer. Record assignment changes and revocations in the audit history. A guest needs a linked user account before an organizer role grants application access.

## Meet setup

Each meet has a club, name, venue, timezone, start and end time, creator, assigned meet organizers, player roster, court availability, partner mode, scoring rule, and scheduling settings. Club organizers retain oversight of every meet within their club. Courts may be available for only part of the session.

The organizer can specify a target number of rounds or matches per player, or ask for an estimate from the session window and expected match duration. The preview must show whether the target fits available court time. Estimated durations cannot guarantee a finish time.

Multiple meets can be in progress simultaneously. Their schedules, timers, and results remain isolated. A shared physical court must not be allocated to overlapping meets. Known registered players assigned to overlapping meets must be flagged and prevented from receiving overlapping match assignments.

## Requirements and acceptance criteria

| ID | Requirement | Acceptance criteria |
| --- | --- | --- |
| PR-01 | Concurrent meets | Two organizers can run separate meets and change results without modifying each other's data. |
| PR-02 | Multiple courts and players | A preview supports any feasible roster and court count; unused players receive explicit rest assignments. Fewer than four eligible players cannot form a match. |
| PR-03 | Partner modes | A meet selects fixed teams, rotating partners (variety or skill balance), or mixed fixed-and-rotating partners before schedule generation. Changes after play begins require an explicit revision and preserve previous matches. |
| PR-04 | Rotating partner fairness | Variety mode favors new partners; balance mode additionally considers skill. The preview exposes repeats and missing partner coverage. |
| PR-05 | Fixed-team fairness | Teams remain intact and face distinct opposing teams where feasible. The preview shows incomplete round-robin coverage and rematches. |
| PR-06 | Equal opportunities | The schedule reports each player's assigned match count and rest distribution; any imbalance is explained. Completed match counts are shown separately during play. |
| PR-07 | Match format | All matches inherit the meet's selected race to 4 games, race to 6 games, or best of 5 games format. Record game totals only; small points, deuces, and tiebreak details are not required. |
| PR-08 | Persistent history | Meet settings, participants, teams, matches, results, timing, and corrections survive refresh and sign-in changes. |
| PR-09 | Reclub roster import | An organizer provides a Reclub webpage URL; the application scrapes its roster, presents the parsed players for review, resolves duplicates, and confirms import. Unsupported or unavailable pages offer manual entry or CSV import. |
| PR-10 | Optional timing | Start and End controls record match timestamps when enabled. When disabled, organizers can still record results and completion. |
| PR-11 | Accounts and identity | A guest can later register and request a verified link to a Reclub identity. A matching display name alone never grants history or account ownership. |
| PR-12 | Replacements and substitutions | Available in every meet mode. A match-specific substitute must already be registered in the current meet; availability does not restrict selection. Add a new player only from the Players list, for the next unstarted match or remaining meet. Preserve actual player history and fixed-team identity where applicable. |
| PR-13 | Optional match statistics | Record each participating player's winners and unforced errors independently. When both are entered, calculate padel ratio as winners minus unforced errors. Missing values remain unknown. |
| PR-14 | Editable skill levels | Authorized organizers can edit player skill levels for a meet. Show skill values and edit controls only when skill balancing is enabled. Preserve values when hidden and use them in subsequent schedule generation; do not silently change published matches. |
| PR-15 | Multiple club organizers | A club supports multiple organizer users. Club-level assignments apply to all meets within that club, with no access implied to other clubs. |
| PR-16 | Meet organizer role | Club organizers can assign users, including players, to organize specific meets. Assigning or revoking a role in one meet must not change permissions in another meet. |

## Main organizer workflow

1. Create a meet and enter session details and court availability.
2. Add players manually or review a Reclub import; identify confirmed players and optional skill levels.
3. Select partner mode, assign fixed teams if applicable, and choose the scoring rule.
4. Generate a preview showing court assignments, rest, match counts, variety, balance, and capacity limitations.
5. Adjust settings or assignments and publish a schedule version.
6. Check players in, run matches, record results, and optionally record timing.
7. Handle absences or court changes by revising only future unlocked assignments.
8. Complete the meet and review actual participation and history.

## Match formats

Confirmed on 31 October 2026:

| Format | Match result entered |
| --- | --- |
| Race to 4 games | First team to win 4 games; record each team's games won. |
| Race to 6 games | First team to win 6 games; record each team's games won. |
| Best of 5 games | First team to win 3 games, with a maximum of 5 games; completed scores are 3–0, 3–1, or 3–2 (or the reverse). |

Race formats use tennis scoring within each game. Small points, deuces, and tiebreak details do not need to be entered. The application records aggregate games won and the match outcome. Best of 5 is games, not sets, and replaces the earlier ambiguous first-to-3 label.

Working interpretation of race formats: reaching the stated target ends the match, without a required two-game margin. On-court point conventions need no detailed configuration or tracking in this release. Store the selected format and target as a match snapshot. A format change after play begins applies only to future matches through a schedule revision. Abandoned matches may have partial game totals and must not be validated as normally completed matches.

## Replacements and substitutes in every meet mode

Offer substitution from court cards, schedule matches, and match details in fixed, rotating, and skill-balanced meets. Preselect the exact unstarted match and restrict outgoing-player choices to its actual participants. Incoming choices must be players already registered in the current meet who are not already in the match. Include players assigned to another match in the round or currently playing. This flow affects only the selected match and cannot create a new player. If no eligible registered player exists, explain why and direct the organizer to the Players list. Started or completed matches cannot be substituted retroactively.

Only the Players list can register a new replacement player. Register and assign that person atomically. An existing registered player is selected from the specific match instead of being added again.

When adding a new replacement from the Players list, the organizer selects the absent player, new incoming player, and one of two scopes:

- **Whole meet:** replace the player for every match if the meet has not started, or for remaining unstarted matches when it is already underway. The change lasts for the rest of this meet only.
- **Next match only:** the substitute occupies the player's place in one selected next unstarted team match. Later matches retain the original player automatically; availability must still be checked before play.

In fixed mode, the team's identity and opposing-team history remain intact. Record who actually played each match. A replacement never rewrites played matches or transfers the absent player's statistics to the incoming player. Mid-match substitution is outside the initial scope.

Match substitution may create overlapping planned assignments; do not filter or reject the choice because of availability. A registered substitute can belong to another fixed team; that player’s other assignments and ongoing team membership remain unchanged. Reject duplicate participants within the same match. Starting a match still requires its players and court to be free. In balanced mode, retain the incoming player’s own skill estimate. Locked future matches require explicit review and unlocking.

A one-match substitute is attached to a specific match ID. If that match is cancelled or replaced during regeneration, flag the substitution for organizer review rather than silently consuming it or moving it to another match.

## Optional match statistics

Provide optional statistics per actual player per match:

| Statistic | Entry or calculation |
| --- | --- |
| Winners | Optional non-negative integer entered by the organizer. |
| Unforced errors | Optional non-negative integer entered by the organizer. |
| Padel ratio | Calculated as winners minus unforced errors; can be negative. |

Use the requested label **padel ratio**, displaying its formula alongside it. This is a difference, not division. Do not include forced errors in the calculation. A blank entry is unknown, not zero; calculate the ratio only when both counts are provided. Explicit zero is a valid entry.

Statistics can be entered or corrected after the result is saved and never block match completion. Keep corrections auditable. Show statistics in match history and player history. Any meet-level summary must state how many matches have statistics; calculate its padel ratio from matches with both values so incomplete data is not presented as a complete meet total.

## Live operation

- Show current courts, next assignments, resting players, and completed matches.
- Make player availability explicit: expected, checked in, temporarily unavailable, or departed.
- Allow cancellation, abandonment, result correction, and unplayed-match replacement with a recorded reason.
- Display scheduled, started, completed, and abandoned participation separately.
- Warn when the next match may exceed the session window; the organizer decides whether to start it.
- Finishing a meet with unresolved matches requires the organizer to classify them as cancelled or abandoned.

## Initial scope boundaries

Payments, court booking, chat, tournament elimination brackets, automated skill ratings, and cross-meet season rankings are outside the initial release. Skill values are optional organizer-provided inputs until a rating model is selected.

## Mixed fixed-and-rotating meet mode

Confirmed on 1 November 2026. Organizers select specific fixed pairs; the remaining players form a rotating pool. Each player belongs to at most one fixed pair. Mixed mode requires at least one fixed pair and two rotating players; all-fixed or all-rotating rosters use their respective modes.

Fixed partners play and rest together. Rotating players partner only with other rotating players, with partner and opponent variety optimized after equal match opportunities. Fixed teams may face rotating teams or other fixed teams. Evaluate fairness across all individual players, not separately for the two groups. Display per-player match counts and any imbalance.

A short session or constrained roster can make equal counts impossible. Explain the compromise, including unused court slots when keeping a fixed pair together requires extra rest. Prefer using all courts when participation is equally fair.

Organizers can choose fixed pairs from Players before play begins; saving regenerates the unstarted schedule. Explicit substitutions remain allowed in every mode under the existing rules. A one-match substitution overrides that assignment only; a new whole-meet replacement inherits the outgoing fixed-pair preference for future matches. No player history or organizer role transfers.

| ID | Requirement | Acceptance criteria |
| --- | --- | --- |
| PR-17 | Mixed partner meet | Selected fixed pairs remain together in generated schedules; remaining players rotate partners. Planned match counts are balanced across both groups, with limitations and unused court slots visible. |

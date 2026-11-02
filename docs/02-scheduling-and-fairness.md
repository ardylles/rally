# Scheduling and fairness

## Definitions

- A match uses four distinct players and one court.
- A round is a group of planned concurrent matches, with remaining players resting.
- A fixed team is a persistent pair within a meet.
- An eligible player is present and available for the proposed slot.
- Partner coverage counts distinct teammates; opponent coverage counts distinct opponents, or distinct opposing teams in fixed mode.

## Hard constraints

Every generated and manually edited schedule must satisfy these constraints:

1. Exactly four distinct eligible players per match, divided into two teams.
2. Generated schedules must not assign a player or court to overlapping matches. Manual match substitutions may create planned player overlaps; never allow a player or court to be used by two live matches.
3. Fixed teams remain together except for organizer-recorded whole-meet replacements or next-match substitutes. Validate the actual pair assigned to each match; incomplete teams cannot play until the absence is resolved.
4. Assignments fit court availability and configured session limits using the planning duration.
5. Started, completed, abandoned, and explicitly locked matches cannot be silently regenerated.
6. Player availability restrictions and configured mandatory rest are respected.
7. Every match has a valid scoring-rule snapshot.

If no valid schedule exists, return the blocking constraints and feasible alternatives. Never silently break a hard constraint to improve fairness.

## Fairness priorities

Optimize in priority order rather than allowing many small preferences to outweigh equal participation:

1. Minimize the difference in match opportunities among equally available players or fixed teams.
2. Reduce excessive consecutive rests and long waits; honor any hard rest constraint.
3. In variety mode, minimize repeated partners, then repeated opponents. In fixed mode, minimize repeated opposing teams.
4. In skill-balance mode, minimize team-strength differences before optimizing partner and opponent variety.
5. Use remaining court capacity without worsening the higher priorities.

Use a reproducible seed to break equivalent choices. Expose tradeoffs in the preview rather than presenting random selection as proof of fairness.

The fairness scope defaults to the current meet. Historical variety across meets is a future option and must not reduce equal opportunity within the current meet without an explicit setting.

## Capacity and feasibility

For N players, C courts, and R complete rounds with all players available, each round can contain at most min(C, floor(N / 4)) matches. If M matches are scheduled overall, there are 4M player appearances. Equal participation requires 4M to be divisible by N; divisibility alone does not guarantee that all other constraints can be satisfied.

For a feasible unconstrained roster, target floor(4M / N) or ceil(4M / N) appearances per player, aiming for a maximum difference of one. With availability, locks, fixed teams, or rest constraints, report any unavoidable larger difference.

Examples:

- Eight players, two courts, seven rounds: 14 matches and seven appearances each. A seven-round partner rotation can give each player all seven other players as partners.
- Ten players, two courts, five rounds: 10 matches and four appearances each, with two players resting each round. Each player has at most four distinct partners, so full partner coverage is impossible in this session.
- Six fixed teams, two courts: a complete single round robin requires 15 matches, five per team. Court capacity gives a lower bound of eight rounds; additional constraints may require more.

For rotating partners, each player needs at least N - 1 appearances to partner with everyone. Covering every unordered pair once requires N(N - 1) / 4 matches, which must be an integer; additional appearances or repeated partnerships may be necessary. These are feasibility checks, not universal construction guarantees.

For T fixed teams, a full single round robin requires T(T - 1) / 2 matches, with T - 1 appearances per team. An odd team count requires byes. An odd player count in fixed mode leaves an unpaired player who must be resolved or marked unavailable before publication.

## Generation approach

1. Validate roster, fixed teams, time windows, scoring rules, and locked assignments.
2. Compute capacity and requested participation targets; report infeasible requests.
3. Build a baseline using team round-robin rotation in fixed mode or balanced player selection in rotating mode.
4. Choose pairings and court assignments against the ordered fairness objectives.
5. Improve the whole proposed schedule through bounded search, swaps, or a constraint solver.
6. Validate all hard constraints independently and calculate the fairness report.
7. Save the seed, input snapshot, scheduler version, and schedule version.

A greedy choice for the next match can create poor later rounds. Evaluate the remaining schedule as a whole. A bounded search must disclose that its best result is not necessarily mathematically optimal.

## Skill balancing

Proposed first model: organizer-entered values on one consistent ordinal scale, using the sum of each team's two values as a simple balance estimate. The scale and acceptable gap still require a product decision.

Unknown skill is unknown, never zero. Ask the organizer to supply estimates or use variety mode. Display estimated team gaps; do not claim that equal sums guarantee equal match quality. Fixed teams cannot be broken up to improve balance.

Authorized club or assigned meet organizers can edit skill values. Hide skill values, edit controls, and balance metrics when the meet does not use skill balancing. Retain stored values when switching modes. Editing a rating does not automatically alter existing assignments; use updated values on explicit schedule generation or future schedule revision while preserving started and completed matches. The static prototype uses a provisional 1–5 scale in increments of 0.5; the production scale remains a product decision.

## Fairness report

Show per-player planned and completed matches, started and abandoned matches, distinct partners and opponents, repeated pairings, resting rounds, maximum consecutive rests, and observed waiting time when timing is enabled. Show team equivalents in fixed mode, plus the estimated strength gap in balance mode.

Display both raw participation and availability context. Late arrivals are compared fairly over the time they were available; they are not promised catch-up matches at the expense of players who attended from the start. Preserve prior participation when calculating future opportunities.

## Live revision and timing

Initial proposal: synchronized rounds. Courts may finish at different times; the next round starts when its courts and players are free. Expected times are estimates and cannot permit a player to start a second match while still playing the first.

On absence, early departure, or court loss, retain played and active matches, retain locks, and regenerate the remaining unlocked schedule from the updated state. Preview the change in participation before publishing a new version.

For all meet modes, support a new replacement from the Players list for the remaining meet or next unstarted match, and an already-registered substitute from a specific match for that match only. Preserve team identity for opposing-team coverage and team match counts. Track individual participation, partner coverage, availability, and statistics against actual players: an incoming player does not inherit the outgoing player's appearances. Include all of an existing participant's earlier appearances when evaluating fairness. Recalculate skill estimates using the actual pair. After a one-match substitution, the original player remains assigned to later matches but must be available to start them.

Keep substitutions attached to their selected match IDs through revisions. If regeneration removes or replaces that match, flag its substitution for review. Never silently substitute into a locked or active match. Report individual fairness changes separately from fixed-team fairness, since replacements can keep team opportunities equal while players' appearance counts differ.

A cancelled match contributes no completed appearances. An abandoned match records partial participation and duration where available. It must not silently count as a full completed match or as no participation; replacement allocation is shown for organizer review.

Starting new matches independently as courts become free is a later scheduling mode because it requires ongoing allocation and wait-time balancing.

## Verification scenarios for implementation

Verify four players on one court; eight on two courts; ten on two courts; more courts than usable groups; odd player counts; odd fixed-team counts; insufficient session time; unknown skills; late arrival; departure; court loss; locked matches; duplicate identities; and overlapping registered-player commitments across meets. Also verify whole-meet replacements before and after play begins, one-match substitutes with automatic return of the original assignment, substitute conflicts with another team, and cancellation or regeneration of a match with a pending substitute.

Automated scheduler checks should enforce all hard constraints, reproduce seeded schedules, verify appearance totals, and compare results against known feasible small cases. Do not test only one expected random ordering.

Match-view substitution must preserve roster size, affect only its selected match, and reject an incoming player already in that match. Players assigned elsewhere in the round or currently playing remain selectable. This manual override may introduce planned overlaps; preserve the other assignments and require free players and court before starting the match. A member of another fixed team may substitute without changing their ongoing membership. Only the Players list can register new replacements.

## Mixed-mode scheduling

Treat each selected fixed pair as one indivisible scheduling unit worth two player appearances. The remaining players form rotating pairs per round. Never partner a rotating player with one member of a fixed pair during generation. Select opponents freely among fixed and rotating teams.

Compare individual match counts across the whole roster first, then improve rest and rotating partner/opponent variety. Among equally fair schedules, prefer more matches and court utilization. Fixed players necessarily repeat partners; exclude those required repeats from rotating-partner variety penalties. Odd rotating pools may need rest and occasional unused court slots. Capacity formulas are upper bounds, not guarantees of equal participation under fixed-pair constraints.

The prototype performs bounded deterministic search over full-capacity and reduced-capacity schedules. It reports actual allocation without claiming mathematical optimality. Validate pair uniqueness and pool size, forbid double assignment in a generated round, and preserve actual participants after manual overrides. Test one and multiple fixed pairs, arbitrary pair selections, odd rosters, insufficient rounds, full-court feasibility, and substitutions.

## Implemented court windows — 2 November 2026

Each named court can have its own availability interval within the session. Rounds start at the meet start and advance by the planned round length, including desired changeover allowance. A court is eligible only when the full slot fits its interval. Fairness is optimized over variable court capacity in all partner modes. Unavailable slots are excluded from unused-capacity counts. Current implementation supports one interval per court on the same calendar day and rejects rounds with no eligible court.

## Early departures and remaining matches

In Players, choose **Leave early**, select the first round the player will miss, and save. The same action records availability and regenerates the remaining schedule atomically. Scheduled departures can be edited from the same row. Players remain in the roster so past participation and statistics are retained.

Schedule also provides **Regenerate remaining**. The boundary is the latest round containing a started or completed match, or round 1 before play begins. Ready matches in earlier rounds are kept; completed/live matches are always kept exactly. Eligible explicit substitutions stay pinned. An explicit registered-player substitution is a match-only attendance override and stays pinned even if the incoming player is marked late or departed. Other unavailable assignments are rebuilt, with prior assignments retained in audit snapshots.

Regeneration seeds player counts and partner/opponent history from preserved matches. Departed players are excluded from future allocation; court windows and meet format still apply. Match counts can differ because of different attendance windows. If too few eligible players remain, no invalid matches are created and the UI explains empty rounds.

- Fixed mode: an incomplete pair sits out. Add a new player from Players with **remaining meet** scope to restore the pair.
- Mixed mode: after a fixed partner leaves, the remaining partner enters the rotating pool.
- A new player added for one match remains limited to that match during regeneration.
- Live matches may finish after a departure is recorded; starting an unstarted match with an unavailable player requires an explicit registered-player substitution for that match.
- Previously recorded substitutions and replacements remain visible in preserved matches and audit snapshots.

Verification: 112 domain/configuration checks and 32 API/PostgreSQL checks passed, including all four modes, live/completed history preservation, court windows, one-match replacements, fixed-pair recovery, permissions and concurrent-edit protection.

## Late arrivals and strict sides

See [attendance and side rules](11-attendance-sharing-administration.md). Late players accrue fairness only from arrival onward, using a virtual baseline rather than catch-up games. Left and Right are strict team constraints; Both is flexible. All four modes, substitutions and replacements enforce compatibility.

Extra rounds append new assignments without regenerating existing rounds. Previous participation counts seed fairness; waiting and departed players stay excluded from automatic selection. Existing slot lengths remain fixed, and session/court extensions must be entered explicitly. See [extra-round controls](11-attendance-sharing-administration.md).

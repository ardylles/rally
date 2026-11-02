# Flexible match management

## Generate one match

Use **Generate match** on the court overview or Schedule, choose a court, and submit. This adds one fair match without changing existing assignments or results. It uses the last occupied round if the chosen court and four compatible players are unassigned there; otherwise it adds a later round. The existing 20-round limit applies.

The generated match has no fixed start/end estimate. Start it whenever the selected court and players are free. Attendance, fixed pairs, strict Left/Right/Both restrictions and fairness still apply. One-match replacement guests do not enter automatic rotation.

Quick matches are pinned during **Regenerate remaining**, provided their teams remain compatible and attendance is valid (including explicit substitution overrides).

## Booking times are advisory

Configured bookings guide automatic court selection. If courts cover the full estimated slot, the scheduler prefers those courts. When none cover the slot, generation can use the registered courts and reports a timing warning instead of refusing the schedule. Manual Generate match can select any registered court, regardless of its planned booking window.

Round estimates can extend beyond session duration. Existing round lengths and times do not move when more rounds are added. Positive court intervals and valid clock values are still required, but bookings need not lie strictly inside the configured session. Estimated times after midnight include a next-day indicator.

Actual start/end buttons remain independent of estimated times. Starting a match still prevents a court or player from being in two live matches.

## Remove, restart and delete

Each court card and schedule/history row has management actions:

- Upcoming: **Remove upcoming match** removes only that match.
- Live or completed: **Restart match** clears its score, player statistics and timer, retaining teams, substitutions and court. It returns to Upcoming; press Start match when ready.
- Live or completed: **Delete match** removes the match and its result from the active meet.

These actions require a confirmation checkbox. Other matches are unchanged. Regenerating a schedule can subsequently generate new matches in emptied slots.

## Regenerate all

**Regenerate all** is available in Schedule even after matches have started or finished. The warning displays match counts and requires explicit acknowledgment.

It replaces the whole active schedule, including completed/live matches, scores, player statistics, manual substitutions and timers. New matches are Upcoming. Meet configuration, roster, attendance, fixed pairs and playing sides remain in effect. If no valid new schedule is possible, the operation is rejected and the original meet remains unchanged.

Use **Regenerate remaining** when completed/live matches should be preserved.

## Authorization and audit

All actions require meet management permission and the current revision. Conflicting edits return 409. Restart, deletion and full regeneration additionally require confirmed=true; the server rejects missing/false acknowledgment.

Every successful mutation records the previous meet document in the existing audit history. This preserves the before-change snapshot, but there is no automatic undo UI.

| POST route | Body |
| --- | --- |
| /api/meets/{id}/generate-match | revision, court (one-based) |
| /api/meets/{id}/matches/{matchId}/restart | revision, confirmed |
| /api/meets/{id}/matches/{matchId}/delete | revision, confirmed |
| /api/meets/{id}/regenerate-all | revision, confirmed |

No relational migration is needed. Match JSON gains manualAssignment, default false for older records.

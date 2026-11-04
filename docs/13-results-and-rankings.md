# Meet results and club rankings

Meet results includes every registered player, including zero appearances. Completed matches credit actual participants after substitutions. Ready/live matches do not count. Rankings order by wins, game difference, then games won; equal totals share rank. Win percentage is displayed but is not a sorting criterion. Different match formats contribute their recorded game totals.

Club history and rankings are available to club members only through GET /api/clubs/{club}/results. This read-only endpoint excludes account emails, audit records, and substitution logs. This week starts Monday; this month starts on the first, through today using each meet’s timezone and scheduled meet date. All time includes all saved results.

Linked accounts aggregate by account ID. Unlinked players aggregate by trimmed, case-insensitive name within the club; use distinct names for different people. Meet-local IDs alone cannot identify players across meets.

Optional winners/errors totals include only entered values. Padel ratio sums winners minus errors only for matches where both were entered. Missing statistics display a dash. Restarted/deleted matches no longer contribute; refreshed pages recalculate from saved matches.

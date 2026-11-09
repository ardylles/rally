# Adding players during a meet

Players → Add player registers an additional participant without replacing anyone. Regenerate remaining matches now is selected by default; uncheck it to change only the roster and regenerate later. Completed/live matches and eligible explicit substitutions remain intact. New players receive fairness from the current scheduling round onward, without catch-up games. Fixed meets add two compatible players as a new fixed pair. Balanced meets require skill; all modes support strict playing sides. The roster limit is 24.

POST /api/meets/{id}/players accepts revision, name, side, skill, regenerate and (for fixed mode) partnerName and partnerSide. Requires meet management permission, rejects stale revisions, and audits player-added. If regeneration has no remaining rounds, add roster-only and generate extra matches later.

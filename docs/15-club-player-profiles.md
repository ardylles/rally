# Club player profiles

Club players are roster identities, separate from registered login accounts and club organizer permissions. Club organizers can create and edit profiles before a player signs up. Members can view the directory; only organizers can edit profiles or link accounts.

## Directory and defaults

Each profile has a required club display name, optional original Reclub name and public player profile URL, Left/Right/Both playing side, optional skill (1–5 in half steps), and optional uploaded photo. Reclub URLs normalize locale prefixes, trailing slash, query strings and username casing; the same Reclub profile can appear only once per club. Names need not be unique.

Left and Right retain the app's strict pairing restrictions. Both is flexible. A saved skill is required when using the profile in a skill-balanced meet. Photos are resized in the browser to a maximum dimension of 256 pixels, encoded as JPEG, and stored with the profile in PostgreSQL. The API accepts JPEG/PNG/WebP with validated signatures and a 200 KB limit. Photos require club access and never appear in public meet responses. No external image URLs are fetched.

Choose Club players in the sidebar. Add a profile manually or prefill its details from a player in the selected meet. Connect an existing meet player attaches its stable club profile identity without changing recorded names, sides, scores or teams.

## Meet use and history

New meet forms offer club player selection alongside the manual roster. Reclub previews carry public profile URLs; matching imports use the saved club display name, side, skill and account link. Names alone never link an import to an account. An imported line whose name was edited no longer automatically matches its original import metadata; choose the saved club profile explicitly in that case.

The directory can add one profile to an existing rotating/mixed meet or two as a fixed pair. Regeneration is optional and preserves started/completed matches under the existing remaining-schedule rules. Duplicate profiles in a meet and profiles from another club are rejected. Profile changes apply to future additions; past meets retain their snapshots. Rankings use clubPlayerId where available, preventing a renamed profile or later account link from splitting its history.

## Account linking

After a player registers, a club organizer enters the exact registered email under Link account. The API looks up an existing active account and adds ordinary club membership if absent. Existing organizer roles are preserved; linking never grants organizer or app-admin privileges. A user account can link to at most one profile in a club.

Blank email unlinks the profile but leaves existing club membership unchanged. Deleting an application account detaches the profile link through a foreign key while preserving the club profile and participant history. Profile mutations use revisions to reject stale edits and record an audit event.

## API and migration

- GET/POST /api/clubs/{club}/players: list/create profiles.
- PUT /api/clubs/{club}/players/{id}: update name, Reclub details, side, skill and optionally photoData. Null/omitted photoData retains the photo; empty string removes it.
- PUT /api/clubs/{club}/players/{id}/account: {revision,email}.
- GET /api/clubs/{club}/players/{id}/photo: member-only image response.
- POST /api/meets/{id}/club-players: {revision,profileIds,regenerate}.
- PUT /api/meets/{id}/players/{playerId}/profile: {revision,clubPlayerId}; club organizers only.

Migration 3 adds club_players and club_player_audit. Account links use ON DELETE SET NULL. Profile data is never inferred from a caller-supplied userId. The existing application startup runs this additive migration under the schema migration lock. Development verification uses the isolated local PostgreSQL database; no production Neon schema or records are changed during development.

Legacy results with an account ID but no club profile ID join that profile’s ranking when the account maps to exactly one profile in the selected results. Conflicting mappings remain separate for explicit organizer review.

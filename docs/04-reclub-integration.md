# Reclub integration

## Status

The agreed integration approach is to scrape Reclub webpages: import a meet's player list from the URL supplied by an organizer and use publicly available profile data to support later member linking. An official API or export is not a prerequisite. This approach was confirmed on 31 October 2026.

Inspected https://reclub.co/m/PATO94 on 2 November 2026. Its public HTML includes a Nuxt payload with participant references and a user map. The parser reads this without executing scripts or calling a Reclub API. The sample yielded 24 entries, including 22 confirmed, matching the visible page. Status 1 is confirmed for this page shape; other codes remain unselected for organizer review.

Implemented: club-organizer preview in Create meet, selecting entries, and copying names into the editable roster. Fetching accepts only HTTPS reclub.co meet paths, disables redirects, limits responses to 2 MB, and times out after 20 seconds. Unsupported markup provides a manual-entry fallback. The application does not save raw pages. External IDs distinguish preview entries but are not persisted on imported players.

The full target workflow below includes pending source provenance, repeat-import reconciliation, CSV import and account linking.

Public profile information can help identify a candidate person; it does not establish that the person creating an account owns that profile.

## Import workflow

1. Organizer pastes a Reclub meet URL.
2. Validate the URL, retrieve the webpage, and scrape its roster. Use HTML parsing when the roster is present in the response; use browser rendering if the public page loads it dynamically.
3. Show source, retrieval time, roster status, and any missing or ambiguous fields.
4. Preview confirmed attendees separately from waitlisted or cancelled entries where the source exposes those distinctions.
5. Match stable external IDs against existing players; flag ambiguous names for review.
6. Organizer selects entries and confirms import into the draft meet roster.
7. Record source attribution and the imported snapshots.

Import only necessary available fields, such as display name, stable external ID, and public profile URL. Do not assume ratings, attendance states, or profile images are available. Retain raw responses only if a documented operational need and retention policy justify it.

Repeated imports should be idempotent for stable identities. A refresh presents additions, removals, and changes for review. It must not automatically remove a player from a live match or erase previous participation. Without stable IDs, require review rather than attempting a confident identity merge.

## Failure behavior

Handle private or login-required pages, invalid URLs, deleted meets, empty rosters, ambiguous attendance states, timeouts, throttling, and changed page structures. Show a useful explanation and retain the existing roster. Support manual entry and CSV import independently.

If URL fetching runs on the server, restrict it to validated provider hosts, validate every redirect, block local/private network destinations, cap response size and timeout, and treat imported content as untrusted data.

## Member registration and account linking

Members create an application account through the application's own authentication. A Reclub claim is a separate action:

1. Member selects or submits a public Reclub profile.
2. Application creates a pending claim and checks for an existing verified link.
3. Present the scraped public profile details as a candidate identity, with the source URL and retrieval time.
4. Use an explicitly designed organizer-assisted review process with appropriate evidence; scraped public data or a name match alone is insufficient to prove ownership.
5. Only after verification associate the account with the player and expose authorized personal history.

One external identity should have at most one active verified owning account. Handle disputed claims, mistaken merges, unlinking, and revocation through an audited process. Never transfer ownership merely because a later user submits the same profile URL.

## Discovery deliverables

- Representative event and profile URLs, including accessibility differences.
- Inspection of event and public profile HTML, including whether browser rendering is needed.
- Extraction rules for roster entries, pagination or lazy loading, and profile links; detect incomplete extraction rather than presenting a partial roster as complete.
- Field map, stable-ID strategy, attendance-state mapping, and duplicate handling.
- A small scraping experiment with representative pages and saved test fixtures for parser regression checks.
- A defined assisted review process for account ownership claims.
- Supported page shapes, scraper failure detection, and fallback user experience.

Reclub import can ship before identity linking if roster access is feasible but ownership verification remains unresolved. The core scheduling product must not depend on either integration being available.

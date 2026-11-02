# Rally static prototype

Open `index.html` in a browser. No build step, application server, database, or installation is required. Fonts optionally load from Google Fonts; system fonts work offline.

## Try the experience

1. Explore Saturday Social: ten players, two courts, five rounds.
2. Start a match, then end it with a game score and optional player statistics.
3. Open History to review the result and calculated padel ratios.
4. Switch to Sunset Doubles, open Players, and try a whole-meet replacement or a next-match substitute.
5. Create a meet to explore player counts, court counts, partner modes, formats, and timing.
6. In any meet type, use Substitute from a court card, schedule row, or match result dialog. Select any player already registered in that meet who is not already in the match; only that match changes. Use Add replacement player in Players to register someone new for the next unstarted match or remaining meet.
7. Choose skill-balanced partners in meet settings, then edit a player's skill in Players. Skill values and edit controls are hidden in other modes. Regenerate an unstarted schedule to use updated ratings.
8. Open Organizers to manage multiple club organizers and assign users/players as organizers for the selected meet. The demo starts with two club organizers; retain at least one.

Changes are saved to this browser's local storage where available. Reset demo restores sample data after confirmation.

## Prototype boundaries

This is standalone HTML/CSS/JavaScript for reviewing the product experience. The application implementation will use the required ReactJS, ASP.NET Core, and PostgreSQL stack.

- Sample names, attendance, venue, and skill values are fictional demonstration data.
- Reclub preview is simulated and performs no scraping.
- Scheduling uses a deterministic best-effort heuristic, not the final constraint solver. Duration is descriptive; court-time feasibility and cross-meet conflicts are not enforced here.
- Started-match conflicts are checked within the selected meet. There is no authentication, account linking, multi-user synchronization, or server persistence.
- Match substitutions use any registered player outside the selected match in every meet mode, including players assigned elsewhere or currently playing. Other assignments stay unchanged; match-start controls still prevent simultaneous live play. New replacements are registered only through Players. Fixed-team identity is retained. Full audit trails and cancelled-match substitution review remain production work.
- Organizer assignments are local prototype data. The interface demonstrates a club organizer's capabilities; real authentication and server-enforced role permissions remain production work. No invitations or external account changes are made.
- Result entry handles normally completed matches. Abandoned matches and detailed availability are not implemented.
- All editable settings regenerate an unstarted meet. Started meets are protected from settings changes.
- The race-format prototype ends at the target without a two-game margin, as documented in the specification.

See the project documents for the complete requirements.

## Verification

JavaScript syntax and direct logic checks passed for 53 roster/mode combinations, including no duplicate player assignments within a round and fixed-team integrity. The default ten-player meet gives four matches each. Checked invalid completed-score rejection, blank statistics, a negative padel ratio, one-match substitution, and rendering each view through a lightweight DOM stub. These are logic checks, not a browser rendering test.

Embedded browser verification was unavailable: the local preview connection timed out, and the browser does not allow file URLs. Open index.html in your own browser to review layout and interactions.

Modification checks: 13 direct logic/render-output checks passed for conditional skill visibility, skill editing and validation, substitution entry points and exact-match targeting, live-match protection, multiple club organizers, last-organizer protection, meet assignment isolation, and organizer-view output. Browser visual verification remains unavailable in this environment.

## Mixed partner meets

Choose Mixed · fixed & rotating in Create meet or settings. Then open Players → Choose fixed pairs to select any disjoint pairs. Keep at least two rotating players. Saving regenerates an unstarted schedule; existing match/substitution history prevents pair reconfiguration. The default fixes the first two players.

Fixed pairs play and rest together; other players rotate partners. All players share the same participation objective. Bounded search considers full and reduced court usage, with unused slots disclosed in the fairness card. It is not a proof of optimality. Ten-player examples with one or two fixed pairs allocate four matches to each player over five rounds on two courts.

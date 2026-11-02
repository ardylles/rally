# Initial API contract

Base path: `/api`. Authentication uses the `rally.session` HttpOnly cookie. JSON requests use camelCase. Send `X-Rally-Request: 1` on mutations; no cross-origin access is enabled.

Errors use an HTTP status and `{"message":"..."}`: 400 invalid input, 401 signed out, 403 no permission, 404 missing resource, 409 stale revision or conflict, and 429 auth throttling.

## Accounts and clubs

| Method | Path | Purpose |
| --- | --- | --- |
| GET | /health | API and PostgreSQL readiness |
| POST | /auth/register | Name, email, password; creates account and signs in |
| POST | /auth/login | Email and password; signs in |
| POST | /auth/logout | Ends current session |
| GET | /auth/me | Current account |
| GET / POST | /clubs | List memberships / create club |
| GET / POST | /clubs/{club}/members | List members / add an existing account by email and role |
| PUT | /clubs/{club}/roles | Set a member's club role; protects the last organizer |
| GET / POST | /clubs/{club}/meets | List visible meets / create and schedule a meet |

Club roles are `organizer` or `member`. Meet organizers are a separate assignment. Only club organizers can manage membership, club roles, meet creation, and meet organizer assignments.

## Meets and matches

| Method | Path | Purpose |
| --- | --- | --- |
| GET | /meets/{id} | Get persisted meet and revision |
| PUT | /meets/{id}/settings | Edit an unstarted meet and regenerate |
| POST | /meets/{id}/generate | Generate an unstarted, unsubstituted schedule |
| PUT | /meets/{id}/pairs | Save fixed pairs and regenerate |
| PUT | /meets/{id}/skills | Edit playerId and skill, without changing current assignments |
| POST | /meets/{id}/replacements | Register name/skill as replacement for outgoingId, scope one or meet |
| POST | /meets/{id}/matches/{match}/start | Start and timestamp a match |
| PUT | /meets/{id}/matches/{match}/result | Save score and optional per-player stats |
| POST | /meets/{id}/matches/{match}/substitute | Replace outgoingId with existing incomingId for this match only |
| GET / PUT | /meets/{id}/organizers | Read / replace assigned organizer userIds |
| GET | /meets/{id}/audit | Latest 100 meet audit entries |

Meet mutation bodies include the current `revision`; successful responses return the new complete meet and incremented revision. An old revision gets 409 and must be refreshed. Organizer assignments are transactionally serialized separately.

Modes: `rotating`, `balanced`, `fixed`, `mixed`. Formats: `4`, `6`, `5` (best of five games, target three). Fixed pairs are arrays of two player IDs. Mixed mode requires at least one fixed pair and at least two rotating players.

Score example:
```json
{
  "revision": 3,
  "score": [4, 2],
  "stats": {
    "player-id": {"winners": 2, "unforcedErrors": 5}
  }
}
```
The API derives padelRatio as -3. Omitted or null counts remain unknown. It never trusts a client-supplied derived ratio.

Roster IDs are meet-local. They do not establish account ownership. Server authorization comes from the cookie identity and membership tables, not fields in the meet document.

## Court availability and Reclub preview

Create/update meet accepts `slotMinutes` (5–120; zero uses legacy auto length) and `courtAvailability`: an array of `{name,start,end}` with HH:mm local times. Array order defines 1-based court numbers. Supply one entry per court or an empty array for full-session availability. All windows must fit the same calendar-day session; every round needs at least one eligible court. Generated matches expose `plannedStart` and `plannedEnd` in the meet timezone. Existing records can have null planned times until regenerated.

`POST /api/clubs/{club}/reclub/preview` requires a club organizer and the normal mutation header. Body: `{ "url": "https://reclub.co/m/PATO94" }`. Response: `{url,name,players:[{id,name,status}]}`. Preview does not save a roster. The client reviews entries before copying selected names into a new meet draft. Status 1 is confirmed; other codes are not selected automatically. Failures: invalid URL 400, unauthenticated/unauthorized 401/403, unsupported page data 422, upstream failure 502, timeout 504, rate limit 429.

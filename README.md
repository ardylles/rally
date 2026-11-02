# Padel Meet

An application for arranging and running fair doubles padel sessions across one or more courts. Multiple meets can operate independently at the same time.

Status: first working ASP.NET Core / React / PostgreSQL application, built 1 November 2026.

## Application

Run `powershell -NoProfile -File scripts/start-dev.ps1` from this folder, then open [Rally](http://127.0.0.1:5080). Create your account and club to start arranging meets.

See [setup, implemented features, and remaining work](docs/07-application-implementation.md) and the [API contract](docs/08-api-reference.md).

The static prototype below remains available as a design reference.

## Neon PostgreSQL

The app is connected to the Rally project’s production branch on Neon. Run these commands from the repository root after pulling the environment variables with neon deploy:

```powershell
npm run build
npm start
```

Open http://127.0.0.1:5082. Run npm run test:neon in another terminal to verify the running application. See [Neon setup, environment variables and verification](docs/10-neon-setup.md). The existing local PostgreSQL workflow remains available.

## Docker

Run the app and PostgreSQL together with Docker Compose. Copy .env.example to .env, set POSTGRES_PASSWORD, then run:

```powershell
docker compose up --build -d
```

Open http://localhost:5081. Database and sign-in keys persist in named volumes. See [Docker setup and hosting](docs/09-docker-deployment.md).

## Static prototype

Open [Rally prototype](prototype/index.html) in a browser to explore sample meets, court rotations, results, optional player statistics, and substitutions. See [prototype notes](prototype/README.md) for demo steps and limitations. Application implementation will follow the confirmed stack below.

## Documents

1. [Product requirements](docs/01-product-requirements.md) — scope, roles, workflows, and acceptance criteria.
2. [Scheduling and fairness](docs/02-scheduling-and-fairness.md) — fixed teams, rotating partners, feasibility, and live changes.
3. [Data and application design](docs/03-data-and-application-design.md) — records, permissions, state transitions, and concurrency.
4. [Reclub integration](docs/04-reclub-integration.md) — roster import, identity linking, and validation work.
5. [Delivery plan and decisions](docs/05-delivery-plan-and-decisions.md) — phased implementation and unresolved product choices.
6. [Technical requirements](docs/06-technical-requirements.md) — required stack and application responsibilities.

## Confirmed technical stack

- Backend: .NET Core, using ASP.NET Core for the application API.
- Frontend: ReactJS.
- Database: PostgreSQL.

Confirmed on 31 October 2026. Exact versions and deployment choices remain to be selected.

## Core principle

Give eligible players a fair number of matches and reasonable rest while offering varied partners and opponents. Explain unavoidable compromises before the organizer publishes a schedule.

In these documents, a **match** is one doubles contest; a **game** is a scoring unit inside that match. Fair participation is measured primarily in matches, since players can complete different numbers of games or minutes in each match.

## Working assumptions

- A meet is one session with a roster, courts, session window, and organizer. Each court can have its own availability window.
- All matches are doubles: two players against two players.
- Fixed partners means persistent teams playing against other teams.
- Rotating partners supports either maximum variety or skill-balanced matches.
- Mixed meets keep selected pairs fixed while the remaining players rotate, with fair match allocation across both groups.
- Fixed teams support whole-meet player replacements and next-match-only substitutes, preserving actual player history.
- Match options are race to 4 or 6 games with tennis scoring, or best of 5 games. Record game totals without requiring small points, deuces, or tiebreak details.
- Optional match statistics include winners, unforced errors, and padel ratio (winners minus unforced errors).
- A mobile-friendly web application is the proposed first interface, using the confirmed .NET Core, ReactJS, and PostgreSQL stack.
- Reclub roster import will scrape the Reclub webpage supplied by the organizer. The supplied sample is supported through a reviewed roster preview in Create meet. Manual roster entry must remain usable independently.

Defaults in the documents are proposals, not decisions already confirmed by the product owner.

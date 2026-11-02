# Technical requirements

## Required stack

Confirmed by the product owner on 31 October 2026.

| Layer | Required technology | Responsibilities |
| --- | --- | --- |
| Backend | .NET Core with ASP.NET Core | Application API, authorization, meet operations, scheduling, scoring validation, Reclub scraping, and account linking. |
| Frontend | ReactJS | Responsive meet setup, roster review, schedule preview, live court board, results, and member history. |
| Database | PostgreSQL | Durable application records, schedule versions, match history, identity links, and audit events. |

Exact framework and database versions will be selected and pinned during implementation setup. The .NET Core requirement describes the backend platform; it does not specify a legacy runtime version.

## Application boundaries

The ReactJS frontend communicates with the ASP.NET Core API. Database credentials and direct PostgreSQL access belong exclusively to the backend. The backend is authoritative for permissions, schedule validity, scoring rules, and state changes, even when the frontend also validates input for convenience.

The scheduling engine is a separate .NET module with explicit inputs and outputs so it can be tested independently of HTTP requests and database access. Publishing a generated schedule remains a transactional application operation.

Reclub scraping runs behind the backend boundary. Fetch and parse public webpages there, using browser rendering if the inspected pages require it. Keep extraction logic separate from player import and identity claims so page changes can be handled without changing meet scheduling.

PostgreSQL stores the records described in [Data and application design](03-data-and-application-design.md). Use versioned migrations, relational constraints, and transactions to protect data integrity. Concurrent edits must be checked on the server and reflected in the database transaction.

## Delivery requirements

- Document local setup for the backend, frontend, and database.
- Keep environment-specific settings and secrets outside committed source files.
- Provide repeatable dependency installation and database migration steps.
- Define the frontend/backend request and response contract, including validation failures and stale-edit conflicts.
- Verify scheduler rules independently, persistence and concurrency against PostgreSQL, and core organizer workflows through the ReactJS interface.
- Preserve server-recorded match timestamps across refreshes; the frontend displays elapsed time from stored values.

## Implementation choices still open

- Exact .NET, React, and PostgreSQL versions.
- JavaScript or TypeScript for React source code.
- Frontend build tooling, component library, and client state management.
- Backend data-access library and migration tooling.
- Authentication provider and session mechanism.
- Live board refresh mechanism, such as polling or server-pushed updates.
- HTML parser and optional browser-rendering tool for Reclub scraping.
- Hosting, deployment packaging, and production backup configuration.

These choices must fit the confirmed stack and do not reopen the three required technologies.

# Docker application and PostgreSQL

The root Dockerfile builds React and ASP.NET Core into one application image. Docker Compose starts that image alongside PostgreSQL 17. Both services start with one command; the database runs in its own container with a persistent volume.

## Run locally

Install Docker Desktop with Linux containers enabled. From the repository root in PowerShell:

```powershell
Copy-Item .env.example .env
```

Edit .env and set POSTGRES_PASSWORD to a unique password. For passwords containing dollar signs, use a single-quoted value in .env to prevent Compose interpolation. The application supplies the password separately from the connection string, so punctuation cannot become connection-string options.

```powershell
docker compose up --build -d
```

Open http://localhost:5081 and register an account. The port can be changed with RALLY_PORT in .env. The default avoids the native development app on port 5080.

This starts a new database. Existing data in .local/postgres is not imported or modified. PostgreSQL is reachable only on the Compose network; its port is not published to the host. The app starts after the database health check succeeds and initializes its schema automatically.

## Operations

```powershell
docker compose ps
docker compose logs -f app
docker compose down
```

Ordinary down/up retains the rally_postgres database volume and rally_keys sign-in key volume. Do not use down --volumes unless you intend to permanently delete both. Changing POSTGRES_PASSWORD in .env does not change a password in an already initialized database: update the database role password as well.

Rebuild after source changes with docker compose up --build -d. The image runs as the non-root app user; credentials and local development files are excluded from the build context.

The database-backed health endpoint is /api/health. To run the existing API integration checks against this installation:

```powershell
$env:RALLY_URL = 'http://127.0.0.1:5081'
node scripts/check-api.mjs
```

These checks create isolated verification accounts and a club.

## Hosting

A Docker-capable machine can run this two-container stack. For public access, place the application behind an HTTPS reverse proxy; the default Compose port is bound to localhost for development. Back up the database volume.

Render can build the root Dockerfile as a Docker web service. Set ConnectionStrings__Rally to the external PostgreSQL connection string (including the provider's required TLS settings), or supply its password separately using Database__Password. Use /api/health as the health-check path. The image listens on Render's PORT environment variable, or 8080 otherwise.

Render's free web service does not run this entire Compose stack or provide persistent disks. Use a managed database such as Neon for that deployment. Without persistent sign-in key storage, restarts can sign users out. A host supporting Docker Compose and persistent volumes can run the included PostgreSQL service instead.

No database password is baked into the image. .env is for local Compose configuration; configure secrets through the hosting provider for a hosted deployment.

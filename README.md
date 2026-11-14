# SecureLogin

SecureLogin is a modular monolith with an ASP.NET Core Web API, an Angular single page application, and PostgreSQL persistence.

## Architecture

- `backend/src/SecureLogin.Domain`: business model boundary; no project dependencies.
- `backend/src/SecureLogin.Application`: application services and use case boundary; references Domain.
- `backend/src/SecureLogin.Infrastructure`: EF Core 10 and Npgsql persistence; references Application and Domain.
- `backend/src/SecureLogin.Api`: HTTP host, health endpoints, and composition root.
- `backend/tests`: unit and integration tests.
- `frontend/secure-login-web`: standalone Angular application with routing.
- `deploy/compose.yaml`: local PostgreSQL service and persistent volume.

The API reads `ConnectionStrings__Postgres` through standard ASP.NET Core environment configuration. `/health/live` reports process health; `/health/ready` checks PostgreSQL. The responses contain status only, never connection details.

## Prerequisites

- .NET 10 SDK
- Node.js 24+ and npm
- Angular CLI (the project pins its CLI in `package.json`; use `npx ng` or `npm run`)
- Docker Engine and Docker Compose

## Start PostgreSQL

From the repository root:

```sh
cp deploy/.env.example deploy/.env
# Set POSTGRES_PASSWORD to a local development value in deploy/.env
docker compose --env-file deploy/.env -f deploy/compose.yaml up -d
```

The named `securelogin-postgres-data` volume preserves the database between restarts. For the local API, set:

```sh
export ConnectionStrings__Postgres='Host=localhost;Port=5432;Database=securelogin_dev;Username=securelogin_dev;Password=YOUR_LOCAL_PASSWORD'
```

Use the same local password configured in `deploy/.env`.

## Run the API

```sh
cd backend
dotnet restore
dotnet run --project src/SecureLogin.Api --urls http://localhost:5080
```

Check `http://localhost:5080/health/live` and `http://localhost:5080/health/ready`.

## Run Angular

```sh
cd frontend/secure-login-web
npm install
npm start
```

The local API base URL is in `src/environments/environment.ts` (`http://localhost:5080`).

## Tests

From `backend/`:

```sh
dotnet test
```

The unit architecture test does not require Docker or PostgreSQL. The integration project has two environment-gated tests:

- HTTP health test runs when `SECURELOGIN_API_URL` is set to a running API URL.
- PostgreSQL connectivity test runs when `ConnectionStrings__Postgres` is set to a reachable database.

For example, start the API and PostgreSQL first, then set both variables before `dotnet test` to run all integration checks. Without them, those tests are reported as skipped.

## Current status

Phase 1 establishes the .NET solution and dependency boundaries, EF Core/Npgsql DbContext registration, API liveness and readiness endpoints, Docker Compose PostgreSQL service, Angular home page, and test scaffolding. No identity or account workflows are implemented.

## Deferred features

ASP.NET Core Identity, user and role models, registration, login, MFA, refresh tokens, recovery codes, authentication middleware, token storage, authorization guards, and production deployment configuration are deferred to later phases.
# CodexProject

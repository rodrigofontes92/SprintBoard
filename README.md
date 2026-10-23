# SprintBoard

SprintBoard is a small project and task management application for small teams. The first delivery establishes a same-origin ASP.NET Core API and React/TypeScript single-page application, with PostgreSQL persistence and cookie-based authentication.

## Stack

- .NET 10 and ASP.NET Core Web API with controllers
- Entity Framework Core 10 with Npgsql and PostgreSQL 18
- ASP.NET Core Identity with an HTTP-only browser session cookie
- React 19, TypeScript 6, and Vite 8
- Node.js 24 LTS for frontend tooling

Architecture decisions are recorded in [`docs/adr`](docs/adr/).

## Prerequisites

- .NET 10 SDK
- Node.js 24 LTS (recommended) or Node.js 22.12+ and its bundled npm
- PostgreSQL 18 running on the local machine
- Git

## Create the local database

Connect to the local PostgreSQL server as an administrator and run:

```sql
CREATE ROLE sprintboard_app LOGIN;
CREATE DATABASE sprintboard_dev OWNER sprintboard_app;
```

Then set a strong password for `sprintboard_app` in `psql`:

```text
\password sprintboard_app
```

The application role owns only the development database. Do not use it as a PostgreSQL administrator.

## Configure the connection string

From the repository root, store the local connection string with .NET user secrets. Replace the password placeholder with the password set above:

```powershell
dotnet user-secrets set --project .\SprintBoard.Api\SprintBoard.Api.csproj "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=sprintboard_dev;Username=sprintboard_app;Password=<your-local-password>"
```

User secrets are stored outside this repository and must never be committed. Production credentials belong in the hosting provider's secret store.

## Create the schema and run the API

From the repository root:

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate --project .\SprintBoard.Api\SprintBoard.Api.csproj
dotnet ef database update --project .\SprintBoard.Api\SprintBoard.Api.csproj
dotnet run --project .\SprintBoard.Api\SprintBoard.Api.csproj --launch-profile http
```

The API listens on `http://localhost:5079`. Its OpenAPI document is available at `/openapi/v1.json` in Development.

## Run the React application

Open a second terminal and run:

```powershell
Set-Location .\sprintboard-web
npm ci
npm run dev
```

Vite serves the UI at `http://localhost:5173` and forwards `/api` requests to the local API. Node 24 LTS is the project baseline; Node 22.12+ also meets Vite's minimum version.

## Build the same-origin deployment bundle

Build the static frontend into the API's `wwwroot` directory, then publish the API:

```powershell
Set-Location .\sprintboard-web
npm ci
npm run build
Set-Location ..
dotnet publish .\SprintBoard.Api\SprintBoard.Api.csproj --configuration Release
```

Outside Development, ASP.NET Core serves the React files from the same origin as the API. Configure HTTPS and the connection string in the hosting environment before deployment.

## Current scope

- Users can register, sign in, sign out, and inspect their session.
- Each authenticated user can create and list only their own projects.
- State-changing browser requests use an antiforgery token.
- Project sharing, tasks, Kanban workflows, email confirmation, hosting, and production account recovery are later project work.

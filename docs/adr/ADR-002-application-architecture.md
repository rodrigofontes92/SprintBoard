# ADR-002: Application Architecture and First Vertical Slice

- **Status:** Accepted for the DEV-16 foundation
- **Date:** 2026-10-23
- **Related decision:** [ADR-001 — Initial SprintBoard Stack](ADR-001-initial-stack.md)

## Context

The project needs an executable foundation that exercises the selected .NET, React, and PostgreSQL stack without adding paid services or splitting a small MVP across multiple deployable services. The first authenticated interaction should also demonstrate resource-level authorization.

## Decisions

### Repository and solution

SprintBoard is a separate repository from Everkeep. The .NET solution uses the XML `.slnx` format, created explicitly with `dotnet new sln --format slnx`. On the installed .NET SDK 10.0.101, the command without `--format` creates a legacy `.sln` file, so the format must remain explicit in setup commands.

The API lives in `SprintBoard.Api`; the React application lives in `sprintboard-web`. Documentation, comments, commits, pull requests, and branch names use English.

### Browser and API topology

In production, ASP.NET Core serves the compiled React files and the API from one origin. During development, Vite serves the UI and proxies `/api` requests to the local API. There is no production Node server and no cross-origin API configuration in the first delivery.

### Identity and browser security

ASP.NET Core Identity stores users in PostgreSQL. A successful sign-in issues an HTTP-only, secure, same-site application cookie. The API returns JSON 401/403 responses instead of redirecting browser API calls to an HTML login page.

The API issues antiforgery tokens and requires the request token header on state-changing endpoints. Authentication cryptography is delegated to ASP.NET Core Identity and ASP.NET Core Data Protection. Production deployment must configure HTTPS, persistent Data Protection keys, email confirmation, and account recovery before public user registration is enabled.

### Resource authorization

Projects are owned by the authenticated user in the first slice. Every project read is constrained by both the project ID and owner ID; new projects take their owner ID from the authenticated principal. The client cannot choose or override ownership. Team membership and project sharing are outside this issue.

### Persistence and migrations

Entity Framework Core with the Npgsql provider maps the Identity schema and application entities to PostgreSQL. Database changes are committed as EF Core migrations. Local credentials are stored with .NET user secrets; no password or connection string with credentials is committed.

### Test runner

The solution uses xUnit v3 with the native Microsoft Testing Platform integration selected in `global.json`. The .NET 10 SDK requires this selection for `dotnet test` to run xUnit v3 through its native test experience.

## Consequences

- The first executable path is registration/sign-in, then creating and listing the signed-in user's projects.
- Same-origin cookies simplify browser authentication, while antiforgery validation protects state-changing requests.
- Public hosting needs a persistent key store and a configured email workflow before production registration is suitable.
- PostgreSQL is required for local execution; the app does not silently switch to a different provider.
- Sharing, task management, Kanban, and integration tests remain later work items.

## Alternatives considered

- **Separate API and frontend origins:** rejected for the first delivery because cross-origin cookie and CORS configuration add complexity without an MVP requirement.
- **Bearer tokens in browser storage:** rejected in favor of the cookie session chosen in ADR-001.
- **A custom password or token implementation:** rejected because ASP.NET Core Identity already provides password hashing, lockout, and cookie integration.

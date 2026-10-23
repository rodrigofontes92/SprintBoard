# ADR-001: Initial SprintBoard Stack

- **Status:** Accepted baseline
- **Decision date:** 2026-10-21
- **Source decision:** [Linear DEV-15 — Validate and record the initial SprintBoard stack](https://linear.app/dev-portefolio-rf/issue/DEV-15/validar-e-registar-a-stack-inicial-do-sprintboard)

## Context

The portfolio plan calls for a small-team project and task application with an API, React UI, authentication and authorization, relational persistence, migrations, tests, and CI. The development machine runs Windows. Everkeep is a separate application and is not part of this decision.

## Decision

| Layer | Baseline |
| --- | --- |
| API | ASP.NET Core Web API with C# controllers, targeting `net10.0`; .NET 10 LTS |
| ORM | Entity Framework Core 10 LTS, patch `10.0.13` |
| Database | PostgreSQL `18.6` |
| .NET provider | Npgsql EF Core provider `10.0.3`, compatible with .NET 10 and EF Core 10 |
| UI | React `19.3.0` single-page application with TypeScript `6.0` and Vite `8.3.4` |
| Web tooling runtime | Node.js `24.14.1`, LTS |
| Browser authentication | ASP.NET Core Identity session cookie, with the API and compiled UI served from one origin |

The API serves the compiled React files in production. During development, Vite proxies UI requests to the API. The first version uses secure, HTTP-only, same-site cookies, antiforgery validation for state-changing requests, and server-side authorization by project. No custom authentication cryptography is permitted.

If the UI and API later use different origins, or SSO becomes a requirement, revisit the authentication decision before implementation. The built-in Identity bearer tokens are platform tokens, not a general-purpose identity server, and are not selected for browser storage.

## Alternatives and trade-offs

- **SQL Server:** familiar to the developer and usable with EF Core, but PostgreSQL adds experience with an open-source database and avoids tying a public demonstration to a paid hosting tier. Managed hosting costs still depend on the provider.
- **A React full-stack framework:** React recommends frameworks for many new applications. This project has a separate ASP.NET Core backend and does not require server-side rendering or SEO, so a Vite SPA keeps the first UI layer small.
- **Node.js Current:** not selected; use the LTS line for application tooling. The current Vite baseline requires Node `20.19+` or `22.12+`; Node 24 LTS satisfies that minimum.
- **TypeScript 5.9:** not selected; TypeScript 6 is stable and compatible with the chosen stack. Keep `strict: true`, avoid removed options, and verify dependencies through the lockfile.

## Costs, licenses, and maintenance

The selected software does not require a license fee for development or commercial use. PostgreSQL and Npgsql use the PostgreSQL License; .NET/EF Core, React, TypeScript, Vite, and Node.js use permissive open-source licenses. Preserve license notices and review transitive dependencies before public release. Hosting is not selected, and zero-cost infrastructure is not assumed.

Use supported patches within the selected compatible lines, commit exact package versions and lockfiles, and keep EF Core and its providers on the same major version. Review versions and security notices during each project phase.

## Risks and mitigations

1. PostgreSQL is new to the developer: keep provider-specific SQL out of application rules and cover persistence with integration tests in the planned test phase.
2. Hosting is not selected: validate cost, .NET support, and SPA hosting before the public demo.
3. Cookie authentication requires CSRF protection and depends on same-origin deployment: test authenticated requests and project authorization.
4. React 19.3 and TypeScript 6 are recent releases: use stable releases, a committed lockfile, and no experimental APIs. The DEV-16 lockfile pins TypeScript `6.0.3`, the latest available patch in the approved 6.0 line on 2026-10-23.

## Baseline commands

Run these commands from the root of the **new SprintBoard repository**, not Everkeep. The format flag is explicit because the installed .NET SDK `10.0.101` creates `.sln` by default when the flag is omitted.

```powershell
dotnet new sln -n SprintBoard --format slnx
dotnet new webapi -n SprintBoard.Api --framework net10.0 --use-controllers
dotnet sln SprintBoard.slnx add SprintBoard.Api/SprintBoard.Api.csproj
dotnet add SprintBoard.Api package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3
dotnet add SprintBoard.Api package Microsoft.EntityFrameworkCore.Design --version 10.0.13
dotnet tool install --global dotnet-ef --version 10.0.13

npm create vite@latest sprintboard-web -- --template react-ts
```

Install PostgreSQL 18 using the [official Windows download](https://www.postgresql.org/download/windows/). The .NET 10 SDK also supports the XML `.slnx` format; pass `--format slnx` explicitly to avoid relying on SDK defaults.

## Official references

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [EF Core releases](https://learn.microsoft.com/en-us/ef/core/what-is-new/), [.NET templates](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates), and [solution file format options](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates#solution-file)
- [Npgsql EF Core 10 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html) and [Npgsql package metadata](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3)
- [PostgreSQL documentation](https://www.postgresql.org/docs/current/), [PostgreSQL license](https://www.postgresql.org/about/licence/), and [SQL Server editions](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025)
- [React versions](https://react.dev/versions), [React app setup](https://react.dev/learn/creating-a-react-app), [TypeScript 6 release notes](https://www.typescriptlang.org/docs/handbook/release-notes/typescript-6-0.html), [Vite releases](https://vite.dev/releases), [Vite guide](https://vite.dev/guide/), and [Node.js release schedule](https://nodejs.org/en/about/previous-releases)
- [Identity API authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0) and [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)

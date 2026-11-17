# Amanet backend

Initial .NET 10 architecture for the legal matter workspace assessment. This change contains infrastructure scaffolding and local tooling only. The assessment PDF is preserved in the repository root.

## Projects and conventions

| Project | Responsibility | References |
| --- | --- | --- |
| Amanet.Core | Future entities, enums and feature-specific repository interfaces | None |
| Amanet.Application | MediatR commands/queries, handlers, DTOs, validators, explicit mappings and service contracts | Core |
| Amanet.Infrastructure | EF Core/Npgsql, entity configurations, migrations, repository and service implementations | Application, Core |
| Amanet.API | Thin versioned controllers, HTTP configuration, ProblemDetails and OpenAPI | Application, Infrastructure |
| Amanet.Tests | Small xUnit/WebApplicationFactory integration suite | API (and its dependencies) |

The reference project's `Commands`, `Queries`, `Handlers/Commands`, `Handlers/Queries`, `DTOs`, `Validations`, `Services`, `Entities`, and `Repositories` conventions are retained. Unused folders are declared in the project files for IDE discoverability; they contain no placeholder classes. Add concrete files as features arrive. Application mappings will be explicit methods, without AutoMapper. Core is intentionally empty until real domain types are introduced.

`ApplicationDbContext` has no Identity or business tables, migrations, seeding, startup database creation, or automatic schema changes. Authentication, document handling, SMTP delivery, and business features come in later changes. Private documents must live outside the web root; the API does not register static-file middleware.

Future feature controllers inherit `BaseController`, which provides `[ApiController]`, API version `1.0`, and the `/api/v{version}/[controller]` route. `/health` is a small endpoint in `Program.cs`. MediatR and FluentValidation validators are registered, but there are no validation pipeline behaviors. Future handlers can invoke validators explicitly where needed.

## Prerequisites and dependencies

- .NET 10 SDK `10.0.400` (or a newer patch in that feature band; see `global.json`). Use a supported, patched .NET 10 runtime.
- Docker Desktop running Linux containers with Docker Compose v2.
- Ports `5109`, `54329`, `54330`, `1025` and `8025` available locally.

Package versions are explicit in project files and transitive graphs are recorded in `packages.lock.json`. MediatR stays at `12.4.1`; EF Core and its CLI tool use `10.0.15`; Npgsql's EF provider uses `10.0.3`. These dependencies run locally without a paid service or commercial license key. Run dependency auditing when restoring or updating; warnings are treated as errors and audit warnings are not suppressed.

```powershell
dotnet restore Amanet.sln --locked-mode
dotnet tool restore
dotnet build Amanet.sln --no-restore
dotnet list Amanet.sln package --vulnerable --include-transitive
```

For an intentional dependency update, edit the exact versions, run `dotnet restore Amanet.sln --force-evaluate`, review lock-file changes, then repeat build, tests and audit. Keep the EF CLI, Design and runtime packages on the same 10.x patch.

## Local services

From the repository root:

```powershell
# Optional: customize the public local-only defaults before first startup.
Copy-Item .env.example .env
docker compose up -d --wait
docker compose ps
```

PostgreSQL is exposed only on `127.0.0.1:54329`; its development database is `amanet_dev`, with user `amanet_local` and password `amanet_local_dev_only`. These are public sample credentials for local use only. The `postgres-data` named volume persists across restarts and `docker compose down`. PostgreSQL 18's volume is mounted at `/var/lib/postgresql`.

Mailpit captures local email on `127.0.0.1:1025`; its web UI is [http://localhost:8025](http://localhost:8025). SMTP is intentionally unauthenticated on loopback. No email sender is implemented in this change. Future local SMTP settings should use host `127.0.0.1`, port `1025`, and no TLS; production needs its own secure configuration.

```powershell
# Stops services while retaining development data.
docker compose down
```

Do not run `docker compose down --volumes` against development data you want to retain. Changing `.env` credentials does not update a database already initialized in the persistent volume; use PostgreSQL administration to rotate them deliberately.

## API configuration and startup

The API runs on the host through `dotnet run`, not in Docker. No database password is stored in tracked API settings. Set the connection string with ASP.NET Core user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=54329;Database=amanet_dev;Username=amanet_local;Password=amanet_local_dev_only" --project Amanet.API
dotnet run --project Amanet.API --launch-profile http
```

Alternatively set a process environment variable before startup (also works outside Development):

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=127.0.0.1;Port=54329;Database=amanet_dev;Username=amanet_local;Password=amanet_local_dev_only'
dotnet run --project Amanet.API --launch-profile http
```

Adjust the connection string if you customize Compose. Missing database configuration fails fast at startup without printing the connection string. User secrets are a development convenience, not a production secret store. Use environment configuration or a deployment secret store outside development. `AllowedHosts` must be configured for the deployment hostname when hosting remotely.

| URL | Behavior |
| --- | --- |
| [http://localhost:5109/health](http://localhost:5109/health) | `200`, `{"status":"Healthy"}`; process liveness only |
| [http://localhost:5109/swagger](http://localhost:5109/swagger) | Swagger UI in Development and Testing |
| [http://localhost:5109/swagger/v1/swagger.json](http://localhost:5109/swagger/v1/swagger.json) | OpenAPI document in Development and Testing |

Liveness does not connect to PostgreSQL or Mailpit and must not be interpreted as dependency readiness. OpenAPI is disabled in other environments. CORS allows exactly `http://localhost:3000`, with any method/header and no credentialed cross-origin cookies.

### Error contracts

HTTP failures use `application/problem+json`, with `status`, `title`, `type`, `instance` and a `traceId`. FluentValidation failures and invalid controller models return `400` with an `errors` dictionary keyed by field/property. Application `ResourceNotFoundException` maps to `404`, `ConflictException` to `409`, and `UnauthorizedAccessException` to `401`. Unexpected failures return a generic `500`; full exceptions are logged server-side. Unmatched routes also produce ProblemDetails. Future authentication challenge/forbid behavior must preserve this contract.

For example, a validation response has this shape:

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/v1/example",
  "traceId": "request-trace-id",
  "errors": { "Name": ["Name is required."] }
}
```

This is a contract example, not an implemented business endpoint.

## Tests

```powershell
# Build first, then run the three HTTP integration tests.
dotnet test Amanet.sln --no-build
```

One test class checks `/health`, OpenAPI, and a `404` ProblemDetails response. Its WebApplicationFactory supplies the separate connection from `Amanet.Tests/appsettings.Testing.json`. These tests do not access PostgreSQL, create databases, or change any schema/data, so Docker is not required to run them.

The optional `postgres-test` Compose profile remains available for future tests that exercise real database behavior. It uses port `54330`, database/user `amanet_test`, password `amanet_local_test_only`, and temporary storage, separate from development:

```powershell
docker compose --profile test up -d --wait postgres-test
docker compose --profile test stop postgres-test
```

## Database access

`ApplicationDbContext` is the scoped EF Core context used by Infrastructure repositories. It already supports change tracking, `SaveChangesAsync`, and explicit transactions through `Database.BeginTransactionAsync`; a separate session wrapper is unnecessary for this scaffold. Keep Application independent of EF Core through feature-specific repository/service contracts as real workflows are introduced. Related business, activity, and notification records must be saved atomically, with cancellation tokens passed to database calls.

## Future migrations

No initial migration is needed for an empty model. When a later feature introduces actual entity configurations, review its migration and apply it explicitly:

```powershell
dotnet tool restore
# Set connection configuration above first. Startup does not apply migrations.
dotnet ef migrations add FeatureName --project Amanet.Infrastructure --startup-project Amanet.API --output-dir Data/Migrations -- --environment Development
dotnet ef migrations script --idempotent --project Amanet.Infrastructure --startup-project Amanet.API -- --environment Development
dotnet ef database update --project Amanet.Infrastructure --startup-project Amanet.API -- --environment Development
```

Review generated SQL, ensure the intended connection/environment, and back up real data before applying schema changes. Validate future relational migrations in isolated PostgreSQL tests. Keep UTC timestamps, cancellation tokens and explicit DTO mappings in later features.

## Local verification (2026-11-17)

- Locked restore and build passed with zero warnings and zero errors using SDK `10.0.400`.
- All three HTTP integration tests passed.
- Transitive NuGet vulnerability audit reported no vulnerable packages in all five projects.
- Docker startup and live API checks were verified during the initial scaffold. The simplified suite runs without a database connection; it does not verify database transactions or business workflows.

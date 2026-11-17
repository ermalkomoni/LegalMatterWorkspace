# Amanet backend

.NET 10 backend for the legal matter workspace assessment. Feature 2 adds Admin authentication with ASP.NET Core Identity and JWT Bearer tokens.

## Structure

| Project | Responsibility |
| --- | --- |
| Amanet.Core | Business entities and repository contracts as features are added |
| Amanet.Application | DTOs, service contracts, commands, queries and validation |
| Amanet.Infrastructure | EF Core/PostgreSQL, Identity, authentication and seeding |
| Amanet.API | Controllers, HTTP configuration and Swagger |
| Amanet.Tests | Basic health and Swagger smoke checks |

Controllers inherit `BaseController` for API version `1.0` and `/api/v{version}/[controller]` routes. The default and fallback policies require the Admin role, including for new controllers. Login and health explicitly allow anonymous access. Public-link endpoints will be added later.

Identity users and roles have integer IDs. The initial migration contains Identity tables only, with no embedded credentials. Private documents will be stored outside the web root; static-file middleware is not enabled.

## Run locally

Prerequisites: .NET SDK `10.0.400` (see `global.json`), Docker Desktop with Linux containers, and PowerShell 7 for the commands below.

From the repository root:

```powershell
dotnet restore Amanet.sln --locked-mode
dotnet tool restore
docker compose up -d --wait
```

Local services:

- PostgreSQL: `127.0.0.1:54329`, database `amanet_dev`, user `amanet_local`, password `amanet_local_dev_only`.
- Mailpit SMTP: `127.0.0.1:1025`; UI: [http://localhost:8025](http://localhost:8025). Email delivery is not implemented yet.

These are public sample database credentials for local use, not Admin login credentials. Optional service overrides go in an ignored `.env` copied from `.env.example`. `docker compose down` retains development data; adding `--volumes` deletes it.

### Configure secrets

The API project already has a `UserSecretsId`. Run these commands from the repository root; secrets are stored outside the repository.

The fictional Admin email is `admin@example.test`. You explicitly choose its password: at least 12 characters including uppercase, lowercase, a digit and punctuation. There is no committed working Admin password or signing key.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=54329;Database=amanet_dev;Username=amanet_local;Password=amanet_local_dev_only" --project Amanet.API
dotnet user-secrets set "Jwt:Issuer" "amanet-local-api" --project Amanet.API
dotnet user-secrets set "Jwt:Audience" "amanet-local-admin" --project Amanet.API
$jwtSigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "Jwt:SigningKey" $jwtSigningKey --project Amanet.API
Remove-Variable jwtSigningKey
dotnet user-secrets set "AdminSeed:Email" "admin@example.test" --project Amanet.API
$adminPassword = Read-Host "Choose the local Admin password (12+ characters, upper/lower/digit/punctuation)" -MaskInput
dotnet user-secrets set "AdminSeed:Password" $adminPassword --project Amanet.API
Remove-Variable adminPassword
```

Adjust the connection string if you changed Compose settings. Startup checks required connection and JWT configuration without printing secrets. Do not commit user-secrets contents, passwords or access tokens.

### Migrate, seed and start

```powershell
dotnet ef database update --project Amanet.Infrastructure --startup-project Amanet.API -- --environment Development
# Creates the Admin role/account if missing, then exits.
dotnet run --project Amanet.API --launch-profile http -- --seed-admin
# Only first account creation needs the seed password.
dotnet user-secrets remove "AdminSeed:Password" --project Amanet.API
dotnet run --project Amanet.API --launch-profile http
```

Repeating the seed command with the same email creates no duplicates and preserves the existing password. Ordinary startup does not seed accounts. Changing the seed email creates another account rather than renaming one.

Development can optionally apply migrations at startup:

```powershell
dotnet user-secrets set "Database:MigrateOnStartup" "true" --project Amanet.API
```

Other environments always require an explicit migration command. Supply configuration through environment variables (`ConnectionStrings__DefaultConnection`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`, and the provisioning-only `AdminSeed__Email`/`AdminSeed__Password`), then run:

```powershell
dotnet ef database update --project Amanet.Infrastructure --startup-project Amanet.API -- --environment Production
dotnet run --project Amanet.API --no-launch-profile -- --environment Production --seed-admin
```

User secrets are for development only. Remove the seed password after provisioning. Use HTTPS when hosting remotely.

## Authentication

| Endpoint | Access |
| --- | --- |
| `POST /api/v1/auth/login` | Anonymous; 10 login requests per minute across the running API |
| `GET /api/v1/auth/me` | Authenticated Admin |
| `/health` | Anonymous process liveness; does not check dependencies |
| `/swagger` | Development and Testing only; includes Bearer authorization |

The API runs at [http://localhost:5109](http://localhost:5109). CORS allows `http://localhost:3000`.

Login request (replace the password placeholder with the one you configured):

```json
{
  "email": "admin@example.test",
  "password": "<your-configured-local-password>"
}
```

Successful response:

```json
{
  "accessToken": "<jwt-access-token>",
  "expiresAtUtc": "2026-11-18T00:00:00Z",
  "admin": { "id": 1, "email": "admin@example.test", "role": "Admin" }
}
```

Send `Authorization: Bearer <jwt-access-token>` on protected requests, or paste the token alone into Swagger's **Authorize** dialog. Login remains anonymous even though Swagger declares Bearer support for the API. `/me` returns just `id`, `email` and `role`.

Identity handles password hashing. Access tokens last eight hours; validation checks issuer, audience, signature and lifetime. Five incorrect password attempts lock an account for 15 minutes. Unknown accounts, incorrect passwords, locked accounts and non-Admin accounts receive the same generic login failure. Excess login requests return `429`. Credentials and tokens are not logged.

There is no registration, refresh token, password reset or client-account API. Tokens remain valid until expiry; `/me` also checks the current account's Admin membership. Public-link access is reserved for later features.

HTTP failures use ProblemDetails with a `traceId`; validation failures also include an `errors` dictionary. Missing/invalid tokens return `401`, and authenticated non-Admins receive `403`.

## Tests

The remaining smoke checks verify health and Swagger availability. They do not connect to a database, seed accounts or exercise authentication. Docker is not required.

```powershell
dotnet test Amanet.sln --no-restore
```

Dependency versions are pinned in project files and lock files. For a package update, restore with `--force-evaluate` and review the lock-file changes. To check for known vulnerabilities:

```powershell
dotnet list Amanet.sln package --vulnerable --include-transitive
```

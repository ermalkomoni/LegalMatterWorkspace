# Amanet backend

.NET 10 backend for the legal matter workspace assessment. Includes Admin authentication, client records, matter management, tasks, internal notes, deadlines, a private PDF document library, and scoped external requests with synchronous SMTP delivery.

## Structure

| Project | Responsibility |
| --- | --- |
| Amanet.Core | Client, matter, task, note, deadline, document/version and activity entities and enums |
| Amanet.Application | DTOs, service contracts, commands, queries and validation |
| Amanet.Infrastructure | EF Core/PostgreSQL, Identity, private PDF storage, authentication and seeding |
| Amanet.API | Controllers, HTTP configuration and Swagger |
| Amanet.Tests | Health/Swagger smoke checks |

Controllers inherit `BaseController` for API version `1.0` and `/api/v{version}/[controller]` routes. The default and fallback policies require the Admin role, including for new controllers. Login, health, and the explicitly scoped public-link controller allow anonymous access.

Identity users and roles have integer IDs. Migrations include Identity, clients, matters, matter activities, tasks, internal notes, deadlines and document versions, with no embedded credentials. Private documents are stored outside the web root; static-file middleware is not enabled.

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
- Mailpit SMTP: `127.0.0.1:1025`; UI: [http://localhost:8025](http://localhost:8025). Development SMTP settings are included; no SMTP credentials are needed for Mailpit.

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

The API runs at [http://localhost:5109](http://localhost:5109). CORS allows the origin of `ExternalLinks:FrontendBaseUrl` (default `http://localhost:3000`), including the `X-Link-Token` header.

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

There is no registration, refresh token, password reset or client-account API. JWTs remain valid until expiry; `/me` also checks the current account's Admin membership. External requests use a separate, narrowly scoped link token, never an Admin JWT.

HTTP failures use ProblemDetails with a `traceId`; validation failures also include an `errors` dictionary. Missing/invalid tokens return `401`, and authenticated non-Admins receive `403`.

## Client and matter management

All endpoints below require an Admin Bearer token. Clients and matters have no hard-delete endpoints.

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET / POST | `/api/v1/clients` | List/search or create clients |
| GET / PUT | `/api/v1/clients/{id}` | Read or update a client |
| GET / POST | `/api/v1/matters` | List/search or create matters |
| GET / PUT | `/api/v1/matters/{id}` | Read or update a matter |
| POST | `/api/v1/matters/{id}/archive` | Archive a matter |
| POST | `/api/v1/matters/{id}/restore` | Restore a matter |
| GET | `/api/v1/matters/{id}/activities` | Read the dated activity history |

List endpoints accept `search`, `page` (default 1), and `pageSize` (default 20, maximum 100). Invalid pagination returns `400`. Responses contain `items`, `totalCount`, `page`, and `pageSize`. Search is case-insensitive and treats search text literally. Clients sort by name then ID; matters sort by opened date descending then ID descending; activities sort by timestamp descending then ID descending.

Matter lists also accept `clientId`, `status`, `priority`, and `archiveState`. Statuses are `Open`, `InProgress`, `OnHold`, `Closed`; priorities are `Low`, `Normal`, `High`, `Urgent`. JSON uses these enum names. `archiveState` is `Active` by default; use `Archived` for only archived matters or `All` to include both. For example:

```text
/api/v1/matters?clientId=1&status=InProgress&priority=High&archiveState=All&pageSize=20
```

Client requests contain `name`, `email`, optional `phone`, and optional `postalAddress`. Matter requests contain `referenceNumber`, `title`, `description`, `primaryClientId`, `status`, `priority`, `openedDate`, and optional `targetCloseDate`. Use fictional clients and matters, such as Alex Example and a fictional lease review. Business dates use `YYYY-MM-DD`; event timestamps are UTC.

References are trimmed, stored uppercase, and protected by a PostgreSQL unique index. Duplicate references return `409`, including concurrent creation. Missing clients/matters return `404`; a target-close date before the opened date returns `400`.

Creation, meaningful updates, archive and restore record activity in the same transaction. An unchanged update or repeated archive/restore does not add another event. Concurrent matter writes return `409`; reload before retrying.

Archived matters and their history remain readable to Admins, but updates return `409`. `MatterRules.EnsureWritable` is the shared rule for task, document and external-link actions. Archiving revokes all current external links in the same database transaction as the archive and its activity. Restore changes only the matter and never reactivates revoked links.

Apply the new migration with the same `dotnet ef database update` command in the setup section.

## Matter tasks, internal notes, and deadlines

All routes below require an Admin Bearer token and are nested under `/api/v1/matters/{matterId}`.

| Method | Endpoint suffix | Purpose |
| --- | --- | --- |
| GET / POST | `/tasks`, `/notes`, `/deadlines` | List or create items |
| GET / PUT / DELETE | `/tasks/{id}`, `/notes/{id}`, `/deadlines/{id}` | Read, edit or delete an item |
| POST | `/tasks/{id}/complete`, `/tasks/{id}/reopen` | Complete or reopen a task |
| POST | `/deadlines/{id}/complete` | Complete a deadline |

Task requests contain `title` (maximum 200 characters), optional `description` (maximum 5000), and optional `dueDate`. Deadline requests contain `title` (maximum 200) and required `date`. Dates use `YYYY-MM-DD`. New items have `Pending` status; completing changes it to `Completed`. Edits preserve the current status.

Task and deadline responses include `isOverdue`: a pending item is overdue only when its date is earlier than the current UTC date. Today, future dates, undated tasks, and completed items are not overdue. Lists return arrays sorted by ID descending.

Note requests contain only `text` (maximum 10000 characters). Responses include the original author's integer `authorId`, taken from the authenticated Admin, and UTC `createdAtUtc` / `updatedAtUtc` timestamps. Editing preserves the author and creation timestamp. Note text is restricted to the dedicated Admin note DTO; matter DTOs and activity descriptions do not contain it. Note activity references the note's ID, including after deletion. No request-body or sensitive EF parameter logging is enabled. Future public-link DTOs must explicitly project their permitted fields and exclude internal notes.

Missing items and IDs belonging to another matter return `404`. Archived matters remain readable; all nested writes return `409`. Meaningful changes and their activity commit atomically, including creates that obtain an item ID before recording activity. Unchanged edits and repeated completion/reopening add no events. Concurrent writes, including an archive racing a nested write, return `409`; reload before retrying.

Apply the `MatterTasksNotesDeadlines` migration using the migration command in the setup section before using these endpoints.

## Private PDF document library

All document endpoints require an Admin Bearer token. Nested routes start with `/api/v1/matters/{matterId}/documents`.

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET | `/api/v1/documents` | Search all matters' documents; optional `matterId` filter |
| POST | Nested document collection | Upload a new document and version 1 |
| GET | `/{documentId}` | Document details |
| PUT | `/{documentId}` | Edit title and category |
| GET | `/{documentId}/versions` | Version history |
| POST | `/{documentId}/versions` | Upload a new immutable version |
| GET | `/{documentId}/versions/{versionNumber}/file` | Preview inline by default; use `?download=true` for an attachment |

Initial uploads use `multipart/form-data` with `title` (required, maximum 200 characters), optional `category` (maximum 100), and `file`. New-version uploads contain only `file`. Metadata edits use JSON containing `title` and optional `category`. Upload responses contain version metadata and the logical `documentId`.

PDF content is limited to **20 MiB (20,971,520 bytes)**. A 21 MiB request limit leaves room for multipart headers; the storage service independently counts the actual file bytes. Oversized content returns `413`. The PDF signature, document structure, and page content must be readable by PDFsharp. Malformed, unsupported, empty, and password-protected PDFs return `400`, including encrypted PDFs that open with an empty password. The content type is determined by validated PDF bytes and stored as `application/pdf`.

The document list accepts `matterId`, `search`, `page` (default 1), and `pageSize` (default 20, maximum 100), returning items and total counts. Search is case-insensitive across title, category, filenames from any version, and the matter's title/reference. Results sort by updated timestamp descending, then ID descending. Version history sorts by version number descending.

Each logical document holds its title, category, matter, classification, and UTC timestamps. Versions retain their sequential number, original display filename (path components/control characters removed), byte length, SHA-256, source (`AdminUpload` or `ClientUpload`), and UTC upload timestamp. Admin uploads have the authenticated uploader's integer ID; anonymous client uploads have a null uploader ID and are attributed through their request/submission. Versions have no edit/delete endpoints; their persisted fields also reject EF updates. A unique database index enforces version numbers per document. Concurrent writes return `409`; reload and retry the upload. Renaming or recategorizing a document does not change version metadata or bytes. Client-request documents receive new versions through the request workflow rather than the Admin upload endpoint.

All document, matter, and version lookups check nested ownership. Mismatched/missing IDs return `404`. Archived matters remain readable; metadata edits and uploads return `409`. Upload metadata and activity are saved in one database transaction. Metadata edits also record activity; unchanged edits do not.

### Private storage and recovery

By default, storage is `private-uploads` under the API content root, outside `wwwroot` and excluded from Git. To use a persistent external directory, set `Documents:StoragePath` through configuration (environment variable `Documents__StoragePath`). Relative paths resolve against the API content root. Storage under the configured web root is rejected. Keep this directory private and mount persistent storage when deploying in a container.

Supplied filenames are display metadata only. Storage uses generated keys, never overwrites an existing key, and never returns keys or filesystem paths in API responses. Only authorized controllers stream content. Preview/download use `application/pdf`, safe inline/attachment filename headers, SHA-256 ETags, last-modified timestamps, `Cache-Control: no-store`, and range processing.

Uploads are written into `.staging`, fully closed and validated, then moved to their final private key before the database save. A failed upload or database save triggers cleanup of its staged and newly published file. The same staging/cleanup path should be used for later generated signature pages so partial generation is never published.

**Filesystem and database operations cannot commit atomically together.** A process crash between file promotion and database commit can leave an orphaned file. A storage failure or ambiguous database commit failure can also require reconciliation. No API serves staging files or discovers files by scanning the directory; a committed version record is required. Missing or wrong-length stored files are unavailable rather than streamed. Back up the database and storage together. During recovery, stop uploads, remove abandoned staging files, and compare final keys with committed version records before removing unreferenced files. Investigate cleanup warnings; there is no background cleanup job.

### PDFsharp and fonts

The cross-platform `PDFsharp` package is pinned to the exact range `[6.2.4]`. The restored package's `.nuspec` declares **MIT** and includes a native `net10.0` asset. Its versioned license is bundled in `Amanet.Infrastructure/Licenses/PDFsharp-6.2.4.txt` and copied to build/publish output. Do not automatically upgrade PDFsharp: newer releases use different licensing terms. Sources: [6.2.4 package](https://www.nuget.org/packages/PDFsharp/6.2.4), [6.2.4 license](https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE), [current license documentation](https://docs.pdfsharp.net/General/License/License.html).

PDFsharp's default PKCS dependency resolves to a version flagged by NuGet audit. A direct `System.Security.Cryptography.Pkcs` 10.0.15 reference supplies the patched dependency without changing PDFsharp. Dependency lock files include this choice.

Lato Regular and Bold are bundled as assembly resources from the [official Google Fonts repository](https://github.com/google/fonts/tree/main/ofl/lato), downloaded on 2026-11-18, with their SIL Open Font License copied to build/publish output. `BundledFontResolver` is registered at startup and resolves `Lato` without OS fonts; italic uses simulation. Later signature pages should use `BundledFontResolver.FamilyName`.

Apply the `PrivatePdfLibrary` migration with the migration command in the setup section. Feature 5 adds no tests; automated coverage is deferred until the end of the assessment.

## Scoped external links and SMTP (Feature 6)

This feature supplies shared request/link infrastructure used by the document request workflow below and later signing workflows. A request has one immutable action (`Upload` or `Sign`) and one current link. There are no client accounts, queues, automatic retries, or background workers. Signature generation remains a later feature; there is no public completion shortcut.

All Admin routes start with `/api/v1/matters/{matterId}/external-requests`:

| Method | Suffix | Purpose |
| --- | --- | --- |
| POST | Collection | Create the request and send its initial invitation |
| GET | Collection | List this matter's requests and delivery attempts for recovery |
| GET | `/{requestId}` | Read progress, link expiry/revocation and delivery attempts |
| POST | `/{requestId}/resend` | Rotate the link and synchronously retry/resend/send a reminder or correction |
| POST | `/{requestId}/revoke` | Revoke the current link; repeated revocation is harmless |

Create JSON contains `action`, `recipientEmail`, `title`, optional `documentVersionId`, `recipientName`, `instructions`, and optional `expiresAtUtc`. Upload requests require a recipient name and must omit the document version. Signing requests require a signable version from the same matter. `title` and `instructions` are intended for the recipient and appear in public context. Resend takes `{}` for the appropriate original invitation, or `{"template":"Reminder"}` / `{"template":"Correction"}`. Correction emails require an existing `CorrectionsRequested` workflow state; use the version-specific review operation below to request corrections. Resend never changes workflow progress or review decisions.

Tokens contain 256 cryptographically random bits, encoded as 64 lowercase hex characters. Only SHA-256 hashes are saved. Links expire after seven days by default (`ExternalLinks:ExpiryDays`, 1–365); initial creation can specify a future `expiresAtUtc` within one year. Every resend/correction rotates the hash and renews the default expiry, invalidating the previous token even when SMTP fails. Resending explicitly revoked or expired requests creates a fresh link for active matters. Upload requests can be resent only in `AwaitingUpload` or `CorrectionsRequested`. Completed signing requests can be resent for read-only access to their final PDF. Delivery attempts never reset workflow progress.

Frontend URLs are generated only for the transient email body:

```text
{FrontendBaseUrl}/external/upload/{requestId}#token=<token>
{FrontendBaseUrl}/external/sign/{requestId}#token=<token>
```

The frontend must read the fragment and send its token in `X-Link-Token` on each API call. Do not put it in an API query string, local analytics, logs or error reports. The backend never returns the token or full URL in Admin DTOs, activity or delivery-attempt records. There is no frontend in this repository yet.

| Public endpoint | Purpose |
| --- | --- |
| `GET /api/v1/external/upload/{requestId}` | Upload request context |
| `GET /api/v1/external/sign/{requestId}` | Signing request context |
| `GET /api/v1/external/sign/{requestId}/file` | Only the request's original PDF before completion, or final signed PDF afterwards |

All public operations require the matching request, action and header token, an unexpired/unrevoked link and an active matter. Invalid, missing, expired, revoked, wrong-action and archived links return the same `404`. Completed requests remain readable through their context; disallowed submissions return `409`. Public context is restricted to request ID, action, recipient-facing title/name/instructions, progress, upload status, current correction explanation, expiry and capability flags. It excludes unrelated matter/client data, notes, email addresses, version IDs and storage keys. Public responses disable caching and referrers. Public calls share a per-remote-IP limit of 30 requests per minute, with no waiting queue; excess calls return `429`.

Workflow services should compose `IExternalRequestService.ValidateAsync` for **every** public operation. It reads a fresh, untracked snapshot; cached EF entities never authorize an expired, revoked, rotated or archived link. Pass `forSubmission: true` for writes; the guard enforces the request's workflow state. `CompleteAsync` completes signing only, while document requests finish through acceptance of a specific submission. To generate a new signed document version, signing services should use an explicit database transaction: save the version to obtain its ID, call `CompleteAsync`, then commit. Signing completion requires a different version of the same logical document; after completion the public file endpoint serves only that final version. Signing services must also reuse `IDocumentService.EnsureSignableAsync` to enforce acceptance of client submissions.

### SMTP configuration and recovery

`IEmailSender` uses MailKit SMTP. Development settings target Mailpit without TLS. For a real SMTP server, configure these through environment variables or user secrets:

```text
ExternalLinks__FrontendBaseUrl=https://your-frontend.example
Smtp__Host=smtp.example
Smtp__Port=587
Smtp__Security=StartTls
Smtp__FromEmail=noreply@example.test
Smtp__FromName=Amanet
Smtp__Username=<smtp-username>
Smtp__Password=<smtp-password>
```

`SslOnConnect` supports implicit TLS, normally on port 465. `None` is permitted only in Development/Testing. TLS certificate validation uses MailKit's defaults. No credentials are committed. `FrontendBaseUrl` must be an absolute HTTPS URL, or HTTP on loopback for local use, without credentials, query or fragment; an optional base path is preserved. Configure the actual frontend URL before deploying. The sender defaults to no SMTP host outside Development; missing/invalid SMTP configuration produces a recoverable failed attempt rather than discarding the request.

Request/link creation and a `Pending` delivery attempt commit **before** SMTP. Create/resend reject caller-owned database transactions so SMTP can never send an uncommitted link. Sending is synchronous and bounded to 30 seconds; after commit it finishes independently of HTTP disconnects. Failed SMTP still returns the created/retried request with a `Failed` attempt. Attempts store template, status, timestamps and only `SmtpDeliveryFailed` / `SmtpTimeout` failure codes. They never store bodies, subjects, URLs, SMTP responses or exception messages. Token-bearing plaintext is constructed only in memory for sending; protocol logging is disabled. `Sent` means SMTP accepted the message, not inbox delivery.

Admin recovery is explicit: list the matter's requests (including when a creation response was lost), inspect delivery attempts, then call resend. A process crash or database failure after SMTP acceptance can leave an attempt `Pending` with an uncertain outcome; resend rotates the link and may deliver another email. No automatic retry is attempted. Revocation/archive racing an in-flight send may deliver an already-invalid link, which remains unusable. Restoration never revives archived links; an Admin may explicitly issue a fresh link after restoration.

Apply the `ScopedExternalLinks` migration with the setup migration command before using these endpoints.

## Client document requests and review (Feature 7)

One request asks one recipient for one PDF. Create it through the existing nested request collection, for example:

```json
{
  "action": "Upload",
  "recipientName": "Alex Example",
  "recipientEmail": "alex@example.test",
  "title": "Lease agreement",
  "instructions": "Upload a readable PDF of the complete agreement."
}
```

`uploadStatus` is separate from link expiry/revocation and SMTP delivery. It follows `AwaitingUpload → Submitted → Accepted`, or `Submitted → CorrectionsRequested → Submitted` when another version is needed. A failed invitation does not discard the request. A failed correction email preserves the review decision and `CorrectionsRequested` state; the Admin can retry with resend. An expired or revoked link does not change the workflow status.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/v1/external-requests?matterId=1&status=Submitted` | Admin list across matters, optionally filtered by matter and upload status |
| GET | `/api/v1/matters/{matterId}/external-requests?status=Submitted` | Existing nested list with optional status filter |
| GET | `/api/v1/matters/{matterId}/external-requests/{requestId}` | Admin request details, submissions and their review history |
| GET | `/api/v1/external/upload/{requestId}` | Public context, status, instructions and current correction explanation |
| POST | `/api/v1/external/upload/{requestId}` | Public multipart upload with one `file` and `X-Link-Token` |
| POST | `/api/v1/matters/{matterId}/external-requests/{requestId}/submissions/{versionId}/review` | Accept or request corrections for that exact submission |
| GET | `/api/v1/notifications?page=1&pageSize=20` | Persisted Admin submission notifications, newest first |

All Admin endpoints require an Admin Bearer token. Reminder/resend and revocation reuse the existing request endpoints. Without an upload-status filter, the shared request lists also include signing requests. To inspect a submitted PDF, use its `documentId` and `versionNumber` from the Admin submission DTO with the existing private document file endpoint; there is no duplicate preview route.

Review uses the submission's **`documentVersionId`**, not its sequential version number. Accept with `{"decision":"Accepted"}`. Request corrections with `{"decision":"CorrectionsRequested","explanation":"Include the missing final page."}`. The explanation is required for corrections and available to the intended client while corrections are outstanding. Each submission can receive one review decision; prior decisions and explanations remain attached to their original versions. Requests in `Submitted` or `Accepted` reject further uploads with `409`; only `AwaitingUpload` and `CorrectionsRequested` allow an upload.

The first upload creates a `ReviewPending` document in the request's matter. Corrections append immutable versions to that same document. Client versions use `ClientUpload` as their source and a null Admin uploader ID. Acceptance changes the classification to `Accepted`. Only the exact accepted submission version is eligible for signing; other versions of that client document are rejected by the shared signing guard. Admin-created documents retain `Standard` classification. Request status/classification cannot be changed through document metadata edits, and the Admin version-upload endpoint cannot bypass client review.

Submission status, document/version metadata, submission history, matter activity and an Admin notification commit in one EF transaction. Review status, the version-bound decision and activity also commit together; corrections include the rotated token and pending delivery attempt in that same commit before SMTP. Upload/review operations own their commit and reject caller-owned or ambient transactions before staging changes or files. Request/matter concurrency tokens reject concurrent uploads, reviews, revocation or archive changes; review lookups also check the persisted unreviewed state so a cached entity cannot overwrite an earlier decision. Archived matter writes are rejected. Public uploads reuse the existing 20 MiB PDF validation, private staging/promotion and failure cleanup. A successful upload returns its committed `Submitted` receipt without another database read or token check that could turn a committed upload into a reported failure. Failure to deliver the response does not delete the stored PDF. The filesystem/database crash-recovery limitation described above still applies.

Notifications are a small persisted Admin inbox: the writer adds a row identifying the request title and version to the submission's unit of work, and the read endpoint provides pagination. There is no notification worker or separate delivery system.

Apply `ClientDocumentRequests` with the setup migration command. It preserves existing tokens/revocations, classifies existing library documents as `Standard`, and maps existing upload links to their initial or previously completed workflow status. No tests were added for Feature 7; validation uses compilation and migration/model checks.

## Tests

The currently included automated checks cover health and Swagger without accessing PostgreSQL or SMTP. Test JWT keys are generated at runtime. No Admin password is required.

```powershell
dotnet test Amanet.sln --no-restore
```

To run only the database-free health and Swagger checks, use `dotnet test Amanet.sln --filter FullyQualifiedName~Amanet.Tests.ApiIntegrationTests`.

Dependency versions are pinned in project files and lock files. For a package update, restore with `--force-evaluate` and review the lock-file changes. To check for known vulnerabilities:

```powershell
dotnet list Amanet.sln package --vulnerable --include-transitive
```

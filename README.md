# Amanet

A legal matter workspace for managing clients, tracking matters and deadlines, and exchanging or signing PDFs with clients.

## Live URL

[Open the Amanet demo](https://amanet-assessment.onrender.com/login)

For a local review use the secrets provided in the link below

## Tech Stack

### Backend

- ASP.NET Core on .NET 10
- Entity Framework Core and PostgreSQL
- ASP.NET Core Identity with JWT authentication
- PDFsharp for generating signed PDFs
- MailKit and Mailpit for local email, Resend for hosted email

**NOTE: I used Clean Architecture with MediatR and CQRS which is a bit of over-engineering for this kind of solution, but I did it on purpose to demonstrate the level of code that I can deliver since .NET is my expertise.**

*Reminder: Mails are not going to be send since Resend requries a custom domain, this is something that I can finish within 30 minutes but I didn't want to over-complicate the solution, as a result emails are only going to appear as notifications on the app.*

### Frontend

- Next.js 16 and React 19
- TypeScript
- Tailwind CSS
- Playwright for browser tests

<a id="run-locally"></a>

## How to Run Locally

### Backend

Use the .NET 10 SDK, Docker with Compose, and PowerShell 7. Compose runs PostgreSQL and Mailpit for you.

From the repository root, restore the dependencies and start both services.

```powershell
dotnet restore Amanet.sln --locked-mode
dotnet tool restore
docker compose up -d --wait postgres mailpit
```

I’ve shared the local development secrets through my app **SnapSend**. It includes the database connection string, JWT settings, and local admin credentials. [Open the Amanet local secrets](https://snapsend.vortex-labs.tech/s/3a0d09f84b8f49bf0a135e22ff790246#key=2MbXyBJeOHdTx5uZXekviruBItSnMKk4lqSah0KKfq4)

Copy the complete JSON from SnapSend, you can paste it manually or from the repository root, type the following command into PowerShell and press Enter. Typing it keeps the JSON on your clipboard for the import.

```powershell
Get-Clipboard -Raw | dotnet user-secrets set --project Amanet.API
```

Apply the database migrations and create the admin account. After the seed command succeeds, remove the setup password from user secrets and start the API.

```powershell
dotnet ef database update --project Amanet.Infrastructure --startup-project Amanet.API -- --environment Development
dotnet run --project Amanet.API --launch-profile http -- --seed-admin
dotnet user-secrets remove "AdminSeed:Password" --project Amanet.API
dotnet run --project Amanet.API --launch-profile http
```

The API exposes Swagger at [http://localhost:5109/swagger](http://localhost:5109/swagger). Document requests and signing invitations appear in the [local Mailpit inbox](http://localhost:8025).

### Frontend

Use Node.js 20.9 or newer and npm. In a second terminal, run the frontend.

```powershell
cd frontend
npm ci
npm run dev -- --port 3000
```

Sign in with the `AdminSeed:Email` and `AdminSeed:Password` from SnapSend. The frontend defaults to `http://localhost:5109/api/v1`. [Open the local workspace](http://localhost:3000/login)

## Business Rules

I enforce the business rules in the backend so the same checks apply from the frontend and when calling the API directly.

- Archived matters are read-only. PDF access is scoped to the matter and the document version.
- Each external link opens one upload or signing request. Links expire, and resending creates a new link that replaces the previous one.
- An admin can accept a client upload or request a correction. Signing a client upload requires the accepted version.
- Each PDF supports one typed-name signer. Signing requires explicit consent and records which version was signed.

## Design Choices

- I separated the domain, application contracts, infrastructure, and API so that the workflow rules have a clear place to live. Controllers handle the HTTP boundary and delegate to services that apply the rules and persist the changes.
- I used scoped links for client uploads and signatures so a client can complete a request without setting up a workspace account. The API stores token hashes and checks the link’s scope and expiry when it’s used.
- For email, I used Mailpit so reviewers can exercise the full workflow locally with fictional addresses. The hosted API sends through Resend. Failed sends stay visible in the request’s delivery history and can be retried by an admin.

## Deployment

| Component | Hosting |
| --- | --- |
| Frontend | Render |
| Backend | Render thorugh API Docker image |
| Database | Render |
| Hosted email | Resend |

The hosted demo can take a moment to wake up. PDFs are stored on the API’s temporary filesystem and can disappear when the service spins down, restarts, or redeploys.


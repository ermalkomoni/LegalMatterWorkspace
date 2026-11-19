# Amanet frontend

Use Node.js 20.9 or later and npm. From `frontend/`, install the locked dependencies and start the development server.

```sh
npm ci
npm run dev -- --port 3000
```

Open [the local frontend](http://localhost:3000). Sign in with the Admin account provisioned by the [backend setup](../README.md#run-locally). The frontend defaults to `http://localhost:5109/api/v1`. To use another API, copy `.env.example` to `.env.local` and set `NEXT_PUBLIC_API_BASE_URL`. If you change the frontend origin, update the backend's `ExternalLinks:FrontendBaseUrl` so its CORS policy accepts requests.

Run `npm run build` to verify a production build. Run `npm start` to serve that build.

Sign-in is at `/login`. The workspace opens to Matters at `/`, with client records at `/clients`. The matter list shows active matters by default. Use the Archive state filter to find and reopen archived matters.

The browser stores the bearer token and its expiry in session storage for the current tab. Keep protected data requests in client components inside the protected layout so they run only after the session is established. The API enforces Admin authorization.

## Folder layout

| Folder | Responsibility |
| --- | --- |
| `src/app/` | Next.js App Router pages and layouts. |
| `src/app/(admin)/` | Admin session layout and sign-in. Workspace routes belong in `(protected)/`. |
| `src/app/external/` | Public link routes under `/external`. |
| `src/features/` | Domain-specific forms, views, and API calls grouped by feature. |
| `src/components/` | UI shared across features. |
| `src/lib/` | Shared configuration and request helpers. |

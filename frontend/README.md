# Amanet frontend

Use Node.js 20.9 or later and npm. From `frontend/`, install the locked dependencies and start the development server.

```sh
npm ci
npm run dev -- --port 3000
```

Open [the local frontend](http://localhost:3000). The scaffold page runs independently of the API. For API-backed features, copy `.env.example` to `.env.local` and set `NEXT_PUBLIC_API_BASE_URL` to the public API base URL. The local default is `http://localhost:5109/api/v1`.

Run `npm run build` to verify a production build. Run `npm start` to serve that build.

## Folder layout

| Folder | Responsibility |
| --- | --- |
| `src/app/` | Next.js App Router pages and layouts. |
| `src/app/(admin)/` | Admin routes grouped without adding a URL segment. |
| `src/app/external/` | Public link routes under `/external`. |
| `src/features/` | Domain-specific forms, views, and API calls grouped by feature. |
| `src/components/` | UI shared across features. |
| `src/lib/` | Shared configuration and request helpers. |

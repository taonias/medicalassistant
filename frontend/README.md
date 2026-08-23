# Medical Assistant Frontend

React + TypeScript frontend for the Medical Assistant doctor workflow.

## Stack

- React 19 + TypeScript
- Vite
- React Router
- TanStack Query (server state)
- Zustand (auth session)
- React Hook Form + Zod (forms)

## Structure

```
src/
├── app/                 # Router and providers
├── features/            # Domain modules (auth, patients, consultations, chat, …)
├── layouts/             # App shell and auth layout
├── shared/              # API client, types, reusable components
├── test/                # Shared MSW server and React Query test harness
└── styles/              # Global styles
```

## Run

1. Start the backend API (`https://localhost:7037`).
2. Install and run the frontend:

```bash
cd frontend
npm install
npm run dev
```

App: `http://localhost:4200`

## Configuration

Copy `.env.example` to `.env`:

```
VITE_API_BASE_URL=https://localhost:7037/api
```

The dev server uses port **4200** to match backend CORS settings.

## Test

```bash
npm test
```

Contract tests live beside the module they protect and use the
`*.contract.test.ts(x)` suffix. Shared browser/network test infrastructure lives
under `src/test`. The suite fixes its backend origin to
`https://backend.test/api`; MSW handles every expected request and fails on any
unhandled request, so local `.env` values do not affect the contract results.

## Backend endpoints used

| Feature | Endpoint |
|---------|----------|
| Login | `POST /api/auth/login` |
| Session | `GET /api/auth/session` |
| Patient | `GET/POST /api/patient` |
| History | `GET /api/patient/{id}/history` |
| Consultations | `GET/POST /api/consultation` |
| Audio upload | `POST /api/consultation/{id}/audio` |
| Transcript | `GET /api/transcript/{consultationId}` |
| Chat | `POST /api/chat/query` |
| Actions | `POST /api/action/trigger` |

## Routes

- `/login`
- `/` — Dashboard
- `/patients` — Patient directory (recent + create)
- `/patients/:patientId` — Patient detail (overview, history, structured data)
- `/patients/:patientId/consultations/new` — Record / upload audio
- `/patients/:patientId/consultations/:consultationId` — Consultation detail
- `/patients/:patientId/chat` — Patient-scoped chat

## Notes

- Patient list uses **recent patients** stored locally because the backend does not expose a paginated patient search endpoint yet.
- Consultation processing status is polled while transcription / structured-data jobs are running.
- Chat action proposals require explicit confirmation before triggering backend actions.

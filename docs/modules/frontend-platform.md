# Frontend Platform

## Purpose

Owns the frontend's bootstrap, routing, and shared infrastructure: everything a feature module needs to run, but nothing about what any feature actually does.

## Public interface / seam

- **Bootstrap/routing/shell**: `frontend/src/app/` — `router.tsx`, `providers.tsx`, `App.tsx`, and `shell/` (`AppShell.tsx`, `AuthLayout.tsx`, `SideNav.tsx`, `FooterNav.tsx`, `UserMenu.tsx`, `RecordButton.tsx`).
- **Transport adapter** (R29): [frontend/src/platform/http/](../../frontend/src/platform/http/) — `core.ts`, `json.ts`, `blob.ts`, `multipart.ts`, `download.ts`, `errors.ts`, `config.ts`. The one sanctioned HTTP client; no feature module talks to `fetch` directly.
- **Media adapter** (R31): [frontend/src/platform/browser-media/](../../frontend/src/platform/browser-media/) — `audioCapture.ts`, `mimeAndFile.ts`. The one sanctioned `MediaRecorder`/microphone boundary.
- **Design system / shared components**: [frontend/src/shared/](../../frontend/src/shared/) — `components/` (`AppBrand`, `Breadcrumbs`, `ConfirmModal`, `EmptyState`, `LoadingSkeleton`, `ConsultationStatusStepper`, etc.), `hooks/`, `utils/`, `types/api.ts`.

## Invariants

- `platform/http` and `platform/browser-media` are the *only* transport and media adapters — a feature module reaching past them to `fetch`/`MediaRecorder` directly would defeat the point of extracting them (R29/R31 both did this specifically to give the rest of the app one seam to mock/replace, not two).
- The frontend module-boundary ESLint rules ([eslint.module-boundaries.js](../../frontend/eslint.module-boundaries.js), R38) do not currently cover `app/`/`platform/`/`shared/` themselves as a protected module — they protect *other* modules' internals from being reached into, not this one's.

## Dependencies

**Owns**: bootstrap, routing, transport/media adapters, design system and CSS foundations.
**Does not own**: any feature's business policy (Patient Workspace, Consultation Lifecycle, etc. each own their own).

## Tests

`frontend/src/app/router.contract.test.tsx`, `frontend/src/platform/http/httpClient.contract.test.ts`, `frontend/src/platform/browser-media/audioCapture.test.ts` — plus the R38 architecture check, [frontend/eslint.architecture.config.js](../../frontend/eslint.architecture.config.js) (`npm run lint:architecture`).

## Runbook & known risks

No dedicated runbook yet — `frontend/README.md` covers dev-onboarding (stack, run command, routes). Known risks tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K08 | Fresh frontend configuration targets HTTPS while root Compose exposes HTTP. |
| K15 | JWT material is persisted in browser localStorage. |
| K25 | Frontend lint has 9 pre-existing errors and the production bundle is approximately 789 KB — not fixed here (R38 deliberately scoped its CI gate around this, see [durable-messaging.md](durable-messaging.md)'s sibling PR discipline note). |
| Q03 | npm audit reports 1 moderate and 4 high frontend dependency advisories. |

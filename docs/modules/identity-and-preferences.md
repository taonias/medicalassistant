# Identity & Preferences

## Purpose

Owns who a Doctor is: session, roles, profile, password, and their theme/preferences — the one place authentication and account-level settings live, distinct from anything about a Patient or Consultation.

## Public interface / seam

- **Backend**: [backend/src/MedicalAssistant.Identity/](../../backend/src/MedicalAssistant.Identity/) is thin by design — `IdentityServicesRegistration.cs` (wires ASP.NET Core Identity + JWT), `IdentityDbInitializer.cs`, `IdentityDbMigrator.cs`. The actual contracts are `IAuthService`/`IUserService` at [Contracts/Identity/](../../backend/src/MedicalAssistant.Application/Contracts/Identity/), and the HTTP surface is `AuthController` (`api/auth/{login,register,session}`, `PUT api/auth/{profile,password}`) at [Modules/CareWorkflow/Identity/AuthController.cs](../../backend/src/MedicalAssistant.Api/Modules/CareWorkflow/Identity/AuthController.cs).
- **Frontend**: `frontend/src/features/auth/` owns the session (`authStore.ts`, `useAuth.ts`, `authApi.ts`); `frontend/src/modules/auth/` is deliberately thin — just the route guards, per the R13 split documented directly in [eslint.module-boundaries.js](../../frontend/eslint.module-boundaries.js)'s own comment ("R13 split auth's route guards into modules/auth while the rest of auth stays at features/auth until a later wave finishes the features/ -> modules/ consolidation"). `frontend/src/features/settings/` owns theme/preferences separately.

## Invariants

- Identity rides on ASP.NET Core Identity (`AddIdentity<ApplicationUser, IdentityRole>`) — no custom password hashing or session store.
- The `features/auth` / `modules/auth` split is an acknowledged, named, in-progress consolidation (R13), not an oversight — don't "fix" it by moving files without re-reading that comment first.

## Dependencies

**Owns**: session, roles, profile, password, theme/preferences, and route guards.
**Does not own**: any HTTP implementation outside its own auth surface, or any patient/consultation workflow.

## Tests

`frontend/src/features/auth/store/authStore.test.ts`, `frontend/src/features/auth/api/authApi.contract.test.ts` on the frontend; no dedicated backend Identity test project — Identity is exercised indirectly through `MedicalAssistant.AcceptanceTests` (login is a precondition for every acceptance scenario).

## Runbook & known risks

No dedicated runbook yet. Known risks tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K14 | Identity password policy permits very short/simple passwords. |
| K15 | JWT material is persisted in browser localStorage. |
| K16 | Frontend session refresh can erase role information. |

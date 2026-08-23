# Browser behavior gates

These Playwright journeys freeze user-visible behavior that component and API contract tests cannot see. Tests are grouped by the capability a developer owns (`routes`, `capture`, `consultations`, and `chat`), while reusable browser-system boundaries live in `fixtures`.

- HTTP is intercepted only at the external API boundary.
- Media-device shims replace browser hardware, not application modules.
- Desktop and mobile projects share each journey so global CSS changes produce deliberate diffs.
- Update screenshots only after confirming the behavior change is intentional: `npm run test:browser:update`.

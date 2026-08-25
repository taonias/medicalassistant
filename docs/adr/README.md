# Architecture Decision Records

This folder holds the accepted ADRs for **Care Workflow** and **Consultation Processing** — the two bounded contexts that live in this repository's `backend/`, `transcriber/`, and root event-bus infrastructure.

**Clinical Knowledge** (the `AI/` service) keeps its own ADR set local, at [`AI/docs/adr/`](../../AI/docs/adr/) — it is a self-contained deployable with its own documentation tree, the same way `backend/README.md` and `frontend/README.md` stay local rather than moving under `docs/`.

See the root [context map](../../CONTEXT-MAP.md) for how the three contexts relate.

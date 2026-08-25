# Domain Docs

This is a multi-context repository. Before changing code, read the root `CONTEXT-MAP.md`, then read the glossary and ADRs for every affected context.

## Canonical context documents

- **Care Workflow**: `docs/contexts/care-workflow/CONTEXT.md`
- **Consultation Processing**: `docs/contexts/consultation-processing/CONTEXT.md`
- **Clinical Knowledge**: `docs/contexts/clinical-knowledge/CONTEXT.md` (see also `AI/CONTEXT.md`, the Clinical Knowledge service's own fuller local glossary)

## Architectural decisions

- Care Workflow/Consultation Processing decisions: `docs/adr/`
- Clinical Knowledge decisions: `AI/docs/adr/`
- The numbered documents under `project-docs/event bus implementations/` are the historical implementation specification for replacing the Azure Function transcriber — superseded as canonical context/ADR source by the paths above, still useful as design history.
- The documents under `project-docs/operations/` provide the system-wide operational and product overview.

Read decisions from every context touched by a cross-context integration. If proposed work conflicts with an accepted ADR, surface the conflict explicitly instead of silently overriding it.

## Vocabulary rules

Use each glossary's canonical terms in issue titles, PRDs, test names, implementation plans, and code-facing domain language. Respect `_Avoid_` synonyms. In particular, keep these concepts distinct:

- Transcription is not Clinical Knowledge Ingestion.
- A Transcript is not a Recording or a Clinical Knowledge Document.
- Document Processing is not audio Transcription.
- A dead-lettered delivery is an operational state, not a Transcription Failed business outcome.

If a needed term is absent or ambiguous, record the gap for domain modeling rather than inventing a synonym.

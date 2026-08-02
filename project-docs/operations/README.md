# Medical Assistant Documentation

This documentation explains what the Medical Assistant is, who it serves, how the system works, what is implemented today, and what remains to make the full product vision operational.

It combines the active repository documentation with a code review performed on 31 July 2026. When a design document and the code disagree, these pages describe the code as the current state and preserve the document as intended direction.

## Start here

| Document | Read this when you want to understand… |
| --- | --- |
| [Product overview](01-product-overview.md) | The problem, audience, product promise, and end-user value |
| [Capabilities and user journeys](02-capabilities-and-user-journeys.md) | What a doctor can do and how the main workflows feel |
| [System architecture](03-system-architecture.md) | Services, boundaries, stores, and communication paths |
| [Component reference](04-component-reference.md) | Each project, its internal modules, and its responsibilities |
| [Domain and data](05-domain-and-data.md) | Core records, ownership, lifecycle states, and data placement |
| [Interfaces and workflows](06-interfaces-and-workflows.md) | HTTP endpoints, queues, and end-to-end processing sequences |
| [Local operations guide](07-running-locally.md) | Prerequisites, configuration, startup order, and troubleshooting |
| [Security, privacy, and clinical safety](08-security-privacy-and-clinical-safety.md) | Trust boundaries, PHI handling, safety mechanisms, and risks |
| [Reliability, observability, and testing](09-reliability-observability-and-testing.md) | Retry behavior, failure handling, telemetry, and test coverage |
| [Current state and gaps](10-current-state-and-gaps.md) | What works, what is isolated, contradictions, and priorities |
| [Documentation source map](11-documentation-source-map.md) | Which repository documents were used and how current they are |
| [Architecture decisions](architecture-decisions.md) | The twelve existing AI architecture decisions in plain language |
| [Context map](CONTEXT-MAP.md) | The system's bounded contexts and canonical domain vocabularies |

The earlier one-page summary remains at [PROJECT_SUMMARY.md](../PROJECT_SUMMARY.md).

## Status language used here

- **Connected**: implemented and invoked by another active component in the current repository.
- **Implemented**: code and tests exist, but the capability may not yet be connected to the doctor-facing application.
- **Target**: described by a PRD/design record but not fully implemented or integrated.
- **Gap**: a missing consumer, incompatible contract, placeholder, or operational/security issue that blocks the intended behavior.

## Product boundary

This is a clinician workflow and patient-record assistance system. It captures and organizes clinical material and is designed to answer questions from the patient's own record with citations. It is not documented here as an autonomous diagnostic system, a treatment recommender, or a replacement for clinical judgment.


# Doctor ↔ AI conversations

Persistent, patient-scoped chat between a doctor and the grounded RAG assistant, with
durable history, live "what the system is doing" progress, and interactive citations.

## Ownership

Conversation history lives entirely in the **backend** `MedicalAssistantDb`; the Clinical
Knowledge (AI) service stays stateless per turn (ADR-0010). The backend persists every turn
and replays context (last-6 verbatim + a rolling summary) into the AI on each ask.

Tables: `Conversations` → `ChatMessages` → `MessageCitations` (cascade delete). A conversation
is scoped to one doctor + one patient (optionally tagged with a consultation).

## Ask flow

`POST /api/chat/ask { conversationId?, patientId?, consultationId?, question, askId }`

- No `conversationId` ⇒ a conversation is auto-created and auto-titled from the first question.
  An explicit **New conversation** (`POST /api/conversations`) creates one first.
- The user turn is persisted immediately; the assistant turn moves
  `Pending → Completed | Refused | Failed`. A refusal (insufficient evidence) is a normal
  answer, not a failure.
- `askId` is the turn's correlation + idempotency key: re-posting the same `askId` returns the
  existing turn; a **failed** turn is re-run in place via
  `POST /api/conversations/{id}/messages/{messageId}/retry` (reusing the `askId`). No auto-retry.
- **The answer is returned whole** on the HTTP response — never token-streamed.

Citations are carried and stored **structurally** (label, chunk/document ids, type, date,
source ref, quote, score) so a thread's grounding re-renders from history. The frontend turns
each verified inline `[E#]` marker into a hover-quote / click-to-expand anchor.

## Rolling summary

After the answer returns, the backend enqueues a best-effort background refresh
(`ConversationSummaryRefreshHostedService`). It is **window-aligned** (`ChatTurnRunner.RecentWindow = 6`):
no summary while the whole thread fits the verbatim window, then a refresh once a window's
worth of new messages has aged out — folding through `maxSequence − window` so the boundary
marches with the trailing edge of the verbatim window (no gap, no overlap; see
`ConversationSummaryWindow.PlanFoldThrough`). The summary itself is produced by the AI's
`POST /patients/{id}/chat/summarize` (DB-seeded `ConversationSummarizer` agent). It is
phrasing/refinement context only — never evidence.

## Live progress (SignalR)

Real phase transitions stream to the asking doctor while a turn is in flight; the answer is
**not** streamed.

```
Browser ──SignalR /hubs/chat (routed by doctorId)──▶ shows the current phase
Backend emits:  ResolvingPatient → PreparingContext
AI emits (via HTTP callback):  SearchingHistory → ComposingAnswer → VerifyingCitations
```

- The browser holds one authenticated hub connection (`/hubs/chat`); the JWT is accepted from
  the `access_token` query string on `/hubs` paths. `DoctorUserIdProvider` routes by the `uid`
  claim so a doctor only sees their own progress.
- The AI service reports its real phase boundaries to the backend over an `X-Api-Key` HTTP
  callback (`POST /api/ai-callback/chat-progress`); the backend relays them onto the hub. Both
  hops are best-effort: a lost progress event never affects the answer, which always arrives on
  the ask's HTTP response.

## Config

The AI → backend callback shares one secret with the backend's `AiCallback:ApiKey`:

| Service            | Setting                          | Compose env               |
| ------------------ | -------------------------------- | ------------------------- |
| backend-api        | `AiCallback:ApiKey`              | `AI_CALLBACK_API_KEY`     |
| clinical-knowledge | `BackendCallback:BaseUrl/ApiKey` | `AI_CALLBACK_API_KEY`     |

Both default to `dev-callback-key` for local development.

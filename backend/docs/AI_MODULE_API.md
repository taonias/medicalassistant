# Python AI Module — HTTP API Contract

This document defines the REST contract between the **MedicalAssistant .NET API** and the **Python AI Module**.

All AI module endpoints require header: `X-Api-Key: {AiModule:ApiKey}`

All callbacks to .NET require header: `X-Api-Key: {AiCallback:ApiKey}`

Correlation IDs must be echoed in callbacks for idempotent processing.

---

## 1. Start Transcription

**POST** `/v1/transcribe`

### Request
```json
{
  "consultationId": 42,
  "audioBlobUri": "https://storage.blob.core.windows.net/consultation-audio/consultations/42/audio/abc.webm",
  "callbackUrl": "https://localhost:7001/api/ai-callback/transcription",
  "correlationId": "a1b2c3d4e5f6"
}
```

### Response (202)
```json
{
  "jobId": "transcribe-job-001"
}
```

### Callback (POST to callbackUrl)
```json
{
  "jobId": "transcribe-job-001",
  "correlationId": "a1b2c3d4e5f6",
  "consultationId": 42,
  "status": "completed",
  "rawText": "Patient reports headache for three days...",
  "transcriptBlobUri": null,
  "failureReason": null
}
```

Status values: `completed`, `failed`, `processing`

---

## 2. Get Transcription Status (polling fallback)

**GET** `/v1/transcribe/{jobId}`

### Response
```json
{
  "jobId": "transcribe-job-001",
  "status": "completed",
  "rawText": "...",
  "failureReason": null
}
```

---

## 3. Extract Structured Data

**POST** `/v1/extract`

### Request
```json
{
  "consultationId": 42,
  "transcriptText": "Patient reports headache...",
  "schemaVersion": "v1",
  "callbackUrl": "https://localhost:7001/api/ai-callback/structured-data",
  "correlationId": "f6e5d4c3b2a1"
}
```

### Response
```json
{
  "jobId": "extract-job-001"
}
```

### Callback
```json
{
  "jobId": "extract-job-001",
  "correlationId": "f6e5d4c3b2a1",
  "consultationId": 42,
  "transcriptId": 10,
  "schemaVersion": "v1",
  "structuredPayload": "{\"summary\":\"...\",\"diagnoses\":[],\"medications\":[],\"vitals\":{},\"allergies\":[]}",
  "status": "completed",
  "failureReason": null
}
```

---

## 4. Chat

**POST** `/v1/chat`

### Request
```json
{
  "message": "Summarize the last three consultations",
  "contextJson": "{ \"patient\": { \"id\": 1, \"firstName\": \"Jane\" }, \"consultations\": [] }",
  "sessionId": "optional-session-id"
}
```

### Response
```json
{
  "answer": "Based on the patient history...",
  "citations": ["Consultation #42", "Consultation #38"],
  "suggestedActions": ["ExtractStructuredData", "SummarizeConsultation"]
}
```

---

## 5. Trigger Action

**POST** `/v1/actions/{actionType}`

Action types: `SummarizeConsultation`, `ExtractStructuredData`, `ChatInsight`, `CustomWorkflow`

### Request
```json
{
  "actionType": "SummarizeConsultation",
  "patientId": 1,
  "consultationId": 42,
  "correlationId": "action-correlation-001",
  "callbackUrl": "https://localhost:7001/api/ai-callback/action",
  "parametersJson": "{}"
}
```

### Response
```json
{
  "jobId": "action-job-001"
}
```

### Callback
```json
{
  "jobId": "action-job-001",
  "correlationId": "action-correlation-001",
  "status": "completed",
  "responsePayload": "{\"summary\":\"...\"}",
  "failureReason": null
}
```

---

## 6. Get Action Status (polling fallback)

**GET** `/v1/actions/{jobId}`

### Response
```json
{
  "jobId": "action-job-001",
  "status": "completed",
  "responsePayload": "{}",
  "failureReason": null
}
```

---

## Idempotency Notes

- .NET stores `CorrelationId` on `ActionRequest` and rejects duplicate triggers for the same doctor by returning the existing record.
- Callback handlers ignore duplicate `completed` status for the same job.
- Consultation creation accepts `Idempotency-Key` header and returns the existing consultation when the key matches.

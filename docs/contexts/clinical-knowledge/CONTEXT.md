# Clinical Knowledge

Clinical Knowledge converts patient-specific clinical material into searchable evidence and answers questions only from that evidence.

## Documents and ingestion

**Document**:
A clinical artifact about a Patient submitted with a declared Document Type.
_Avoid_: Upload, file, record

**Document Type**:
The caller-declared kind of Document: Session Transcript, Doctor Note, Lab Report, or Imaging Report.
_Avoid_: Classification, inferred type

**Ingestion**:
One tracked attempt to process a Document into derived searchable material.
_Avoid_: Upload, import, job

**Ingestion Strategy**:
The processing pipeline selected for one Document Type.
_Avoid_: Classifier, generic pipeline

**Orchestrator**:
The deterministic router from Document Type to Ingestion Strategy.
_Avoid_: Agent, classifier

**Correction**:
A changed resubmission of an existing Document identity that supersedes the earlier derived material.
_Avoid_: Update, re-upload

**Continuation**:
A new Transcript sequence for an existing Session that adds sibling material without superseding earlier sequences.
_Avoid_: Correction, append

**Un-ingest**:
Removal of one Document and its derived material while retaining an accountable tombstone.
_Avoid_: Erasure, delete

**Erasure**:
Administrative removal of all Clinical Knowledge data for one Patient, including tombstones.
_Avoid_: Un-ingest, purge

## Derived knowledge

**Chunk**:
A semantically coherent, provenance-bearing span derived from a Document and used as the unit of embedding and retrieval.
_Avoid_: Passage, segment

**Context Blurb**:
A short model-written description used only to improve a prose Chunk's embedding context.
_Avoid_: Header, source text

**Document Summary**:
A model-written summary of one Document, stored and embedded as a clearly identified summary Chunk.
_Avoid_: Patient Summary, transcript text

**Patient Summary**:
A rolling overview derived from the current Document Summaries for one Patient.
_Avoid_: Conversation summary, Patient Record

**Panel**:
A group of laboratory measurements rendered together as one searchable Chunk.
_Avoid_: Table, analyte

**Analyte Result**:
A single lab measurement whose displayed value, unit, range, and flag are copied verbatim from extracted source cells.
_Avoid_: Generated fact, panel

## Retrieval and answers

**Evidence Candidate**:
A patient-scoped vector-search hit before the confidence threshold is applied.
_Avoid_: Citation, Evidence Item

**Evidence Item**:
A Chunk that cleared the confidence threshold and is available to ground one answer.
_Avoid_: Evidence Candidate, generated fact

**Confidence Threshold**:
The configured minimum similarity required for an Evidence Candidate to become an Evidence Item.
_Avoid_: Relevance guarantee, hard-coded score

**Grounded Answer**:
Model-written clinical prose constrained to Evidence Items supplied for the current question and accompanied by citations.
_Avoid_: General medical answer, completion

**Insufficient Evidence**:
A successful refusal stating that the Patient's record does not support an answer.
_Avoid_: Error, empty result

**Citation**:
A reference from a Grounded Answer to a supplied Evidence Item, carrying source identity and a bounded verbatim quote.
_Avoid_: Evidence Candidate, external reference

**Conversation Context**:
Bounded earlier chat information supplied only to interpret the current question, never stored or treated as medical evidence.
_Avoid_: Evidence, conversation memory owned here

# Consultation Processing

Consultation Processing turns a stored consultation artifact into clinical text and announces the result to downstream capabilities.

## Language

**Consultation File**:
An audio recording or document stored for a Consultation and ready for asynchronous processing.
_Avoid_: Queue message, blob, job

**Consultation Audio Uploaded**:
The fact that an audio Consultation File has been stored successfully and is available for Transcription.
_Avoid_: Transcribe command, generic file uploaded

**Consultation Document Uploaded**:
The fact that a non-audio Consultation File has been stored successfully and is available for Document Processing.
_Avoid_: Transcript ready, transcribe document

**Transcription**:
The process of deriving editable clinical text from a Consultation's audio recording.
_Avoid_: Ingestion, extraction, upload

**Transcript**:
The editable textual record produced by Transcription for one Consultation.
_Avoid_: Recording, AI summary, document

**Transcript Ready**:
The fact that a Transcript has been stored successfully and is available to downstream clinical-knowledge processing.
_Avoid_: Transcription completed command, AI ingestion

**Transcription Failed**:
The fact that Transcription reached a terminal failure after its retry policy was exhausted.
_Avoid_: Exception, dead-letter, transient failure

**Document Processing**:
The process of extracting clinical content from a non-audio Consultation File.
_Avoid_: Transcription, speech recognition

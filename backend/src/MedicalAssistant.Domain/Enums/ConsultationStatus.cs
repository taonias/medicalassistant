namespace MedicalAssistant.Domain.Enums;

public enum ConsultationStatus
{
    Draft = 0,
    AudioUploaded = 1,
    Transcribing = 2,
    Transcribed = 3,
    StructuredDataPending = 4,
    Completed = 5,
    Failed = 6,
    DocumentUploaded = 7
}

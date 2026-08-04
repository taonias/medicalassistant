namespace MedicalAssistant.EventBus.Contracts;

public static class ConsultationIntegrationEvents
{
    public const string AudioUploadedV1 = "consultation.audio-uploaded.v1";
    public const string DocumentUploadedV1 = "consultation.document-uploaded.v1";
    public const string TranscriptReadyV1 = "consultation.transcript-ready.v1";
    public const string TranscriptionFailedV1 = "consultation.transcription-failed.v1";
    public const string DeletedV1 = "consultation.deleted.v1";

    public static IntegrationEventContractRegistry Registry { get; } =
        IntegrationEventContractRegistry.Create(builder => builder
            .Add<ConsultationAudioUploadedV1>(AudioUploadedV1, 1)
            .Add<ConsultationDocumentUploadedV1>(DocumentUploadedV1, 1)
            .Add<ConsultationTranscriptReadyV1>(TranscriptReadyV1, 1)
            .Add<ConsultationTranscriptionFailedV1>(TranscriptionFailedV1, 1)
            .Add<ConsultationDeletedV1>(DeletedV1, 1));
}

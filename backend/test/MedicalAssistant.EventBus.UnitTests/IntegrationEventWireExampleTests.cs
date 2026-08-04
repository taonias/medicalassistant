using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.EventBus.UnitTests;

public class IntegrationEventWireExampleTests
{
    public static IEnumerable<object[]> Examples()
    {
        yield return
        [
            "consultation.audio-uploaded.v1.json",
            new IntegrationEventEnvelope<ConsultationAudioUploadedV1>(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ConsultationIntegrationEvents.AudioUploadedV1,
                1,
                ExampleTime,
                "medicalassistant.backend",
                "corr-example",
                "cause-example",
                new ConsultationAudioUploadedV1(101, "file-audio-101", "audio/wav", "private://consultations/101/audio", 120))
        ];
        yield return
        [
            "consultation.document-uploaded.v1.json",
            new IntegrationEventEnvelope<ConsultationDocumentUploadedV1>(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ConsultationIntegrationEvents.DocumentUploadedV1,
                1,
                ExampleTime,
                "medicalassistant.backend",
                "corr-example",
                "cause-example",
                new ConsultationDocumentUploadedV1(102, "file-document-102", "referral", "application/pdf", "private://consultations/102/document"))
        ];
        yield return
        [
            "consultation.transcript-ready.v1.json",
            new IntegrationEventEnvelope<ConsultationTranscriptReadyV1>(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ConsultationIntegrationEvents.TranscriptReadyV1,
                1,
                ExampleTime,
                "medicalassistant.transcription-worker",
                "corr-example",
                "cause-example",
                new ConsultationTranscriptReadyV1(103, "file-audio-103", 203, 2, "el-GR"))
        ];
        yield return
        [
            "consultation.transcription-failed.v1.json",
            new IntegrationEventEnvelope<ConsultationTranscriptionFailedV1>(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                ConsultationIntegrationEvents.TranscriptionFailedV1,
                1,
                ExampleTime,
                "medicalassistant.transcription-worker",
                "corr-example",
                "cause-example",
                new ConsultationTranscriptionFailedV1(104, "file-audio-104", "unsupported-audio-format", "Permanent"))
        ];
        yield return
        [
            "consultation.deleted.v1.json",
            new IntegrationEventEnvelope<ConsultationDeletedV1>(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                ConsultationIntegrationEvents.DeletedV1,
                1,
                ExampleTime,
                "medicalassistant.backend",
                "corr-example",
                "cause-example",
                new ConsultationDeletedV1(105, ExampleTime, "clinician-request"))
        ];
    }

    private static readonly DateTime ExampleTime = new(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [MemberData(nameof(Examples))]
    public void Contract_serialization_matches_committed_wire_examples<TPayload>(
        string exampleFileName,
        IntegrationEventEnvelope<TPayload> envelope)
    {
        var expected = File.ReadAllText(Path.Combine(ExamplesDirectory(), exampleFileName)).TrimEnd();

        Assert.Equal(expected, IntegrationEventSerializer.Serialize(envelope));
    }

    [Fact]
    public void Deserialization_tolerates_optional_additions()
    {
        var json = File.ReadAllText(Path.Combine(
            ExamplesDirectory(),
            "consultation.audio-uploaded.v1.json"));
        json = json.Replace(
            "\"durationSeconds\":120}",
            "\"durationSeconds\":120,\"futureOptionalField\":\"ignored\"}");

        var envelope = (IntegrationEventEnvelope<ConsultationAudioUploadedV1>)IntegrationEventSerializer.Deserialize(
            json,
            ConsultationIntegrationEvents.Registry.Resolve<ConsultationAudioUploadedV1>());

        Assert.Equal(101, envelope.Payload.ConsultationId);
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Examples_do_not_include_forbidden_clinical_fields<TPayload>(
        string exampleFileName,
        IntegrationEventEnvelope<TPayload> _)
    {
        var json = File.ReadAllText(Path.Combine(ExamplesDirectory(), exampleFileName));

        Assert.DoesNotContain("transcriptText", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doctorId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fileName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signedUrl", json, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExamplesDirectory() => Path.Combine(
        RepositoryRoot(),
        "backend",
        "src",
        "MedicalAssistant.EventBus",
        "Contracts",
        "Examples");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}

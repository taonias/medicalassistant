namespace MedicalAssistant.AcceptanceTests;

/// <summary>
/// Packaged-image gate for the root Compose deployment. It is opt-in locally
/// because it builds and starts the production images; CI always enables it.
/// </summary>
public sealed class RootComposeConsultationProcessingTests
{
    [Fact]
    public async Task Root_Compose_completes_Transcription_and_Clinical_Knowledge_Ingestion_with_empty_and_existing_volumes()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_ROOT_COMPOSE_GATE"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var environment = new RootComposeEnvironment();
        ScriptSuccessfulTranscriptionAndIngestion(environment, "Document created from empty volumes.");
        await environment.StartWithEmptyVolumesAsync();

        var first = await ProcessRecordingAsync(
            environment,
            "patient-r06-compose-empty",
            "R06-COMPOSE-EMPTY");

        ScriptSuccessfulTranscriptionAndIngestion(environment, "Document created from retained volumes.");
        await environment.RestartWithExistingVolumesAsync();

        var second = await ProcessRecordingAsync(
            environment,
            "patient-r06-compose-existing",
            "R06-COMPOSE-EXISTING");
        using var clinicalKnowledge = environment.CreateClinicalKnowledgeClient();
        var persisted = await clinicalKnowledge.WaitForCompletedDocumentAsync(
            first.Patient.ExternalPatientId,
            first.Consultation.Id.ToString(),
            TimeSpan.FromSeconds(15));

        Assert.Equal("Completed", first.Transcript.Status);
        Assert.Equal("Completed", first.Document.Status);
        Assert.Equal("Completed", second.Transcript.Status);
        Assert.Equal("Completed", second.Document.Status);
        Assert.Equal("Completed", persisted.Status);
    }

    private static async Task<CompletedConsultation> ProcessRecordingAsync(
        RootComposeEnvironment environment,
        string externalPatientId,
        string recordingCanary)
    {
        var doctor = await environment.CreateDoctorClientAsync();
        var patient = await doctor.CreatePatientAsync(externalPatientId);
        var consultation = await doctor.CreateConsultationAsync(patient.Id);
        await doctor.UploadAudioAsync(
            consultation.Id,
            SyntheticRecording.Wave(recordingCanary));
        var transcript = await doctor.WaitForTranscriptAsync(
            consultation.Id,
            TimeSpan.FromSeconds(45));
        using var clinicalKnowledge = environment.CreateClinicalKnowledgeClient();
        var document = await clinicalKnowledge.WaitForCompletedDocumentAsync(
            patient.ExternalPatientId,
            consultation.Id.ToString(),
            TimeSpan.FromSeconds(45));
        return new CompletedConsultation(patient, consultation, transcript, document);
    }

    private static void ScriptSuccessfulTranscriptionAndIngestion(
        RootComposeEnvironment environment,
        string summary)
    {
        environment.Speech.EnqueueTranscript(
            "Doctor: This is a controlled follow-up.\nPatient: The symptoms are improving.");
        environment.Models.EnqueueTranscriptPlan(
            "Controlled follow-up with improving symptoms.",
            summary);
    }

    private sealed record CompletedConsultation(
        PatientView Patient,
        ConsultationView Consultation,
        TranscriptView Transcript,
        ClinicalKnowledgeDocumentView Document);
}

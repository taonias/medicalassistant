namespace MedicalAssistant.AcceptanceTests;

/// <summary>
/// Characterizes the asynchronous production path with the same service
/// boundaries as the root Compose stack. Only external providers are scripted.
/// </summary>
public sealed class FullSystemAcceptanceTests
{
    [Fact]
    public async Task Uploaded_Recording_completes_Transcription_and_Clinical_Knowledge_Ingestion()
    {
        await using var environment = new AcceptanceEnvironment();
        environment.Speech.EnqueueTranscript(
            "Doctor: How have the headaches changed?\nPatient: They are less frequent this week.");
        environment.Models.EnqueueTranscriptPlan(
            contextBlurb: "Follow-up on improving headaches.",
            summary: "Headaches are less frequent at follow-up.");
        await environment.StartFullSystemAsync();

        var doctor = await environment.CreateDoctorClientAsync();
        var patient = await doctor.CreatePatientAsync("patient-r06-empty-volume");
        var consultation = await doctor.CreateConsultationAsync(patient.Id);

        await doctor.UploadAudioAsync(
            consultation.Id,
            SyntheticRecording.Wave("R06-EMPTY-VOLUME-AUDIO"));

        var transcript = await doctor.WaitForTranscriptAsync(
            consultation.Id,
            TimeSpan.FromSeconds(30));
        Assert.Equal("Completed", transcript.Status);

        using var clinicalKnowledge = await environment.CreateClinicalKnowledgeClientAsync();
        var document = await clinicalKnowledge.WaitForCompletedDocumentAsync(
            patient.ExternalPatientId,
            consultation.Id.ToString(),
            TimeSpan.FromSeconds(30));

        Assert.Equal("SessionTranscript", document.DocumentType);
        Assert.Equal("Completed", document.Status);
    }

    [Fact]
    public async Task A_new_Recording_completes_Transcription_and_Ingestion_after_restart_against_existing_volumes()
    {
        await using var environment = new AcceptanceEnvironment();
        ScriptSuccessfulTranscriptionAndIngestion(environment, "Initial document before restart.");
        await environment.StartFullSystemAsync();

        var firstDoctor = await environment.CreateDoctorClientAsync();
        var existingPatient = await firstDoctor.CreatePatientAsync("patient-r06-existing-data");
        var existingConsultation = await firstDoctor.CreateConsultationAsync(existingPatient.Id);
        await firstDoctor.UploadAudioAsync(
            existingConsultation.Id,
            SyntheticRecording.Wave("R06-BEFORE-RESTART"));
        await firstDoctor.WaitForTranscriptAsync(existingConsultation.Id, TimeSpan.FromSeconds(30));
        using (var clinicalKnowledge = await environment.CreateClinicalKnowledgeClientAsync())
        {
            await clinicalKnowledge.WaitForCompletedDocumentAsync(
                existingPatient.ExternalPatientId,
                existingConsultation.Id.ToString(),
                TimeSpan.FromSeconds(30));
        }

        ScriptSuccessfulTranscriptionAndIngestion(environment, "New document after restart.");
        await environment.RestartFullSystemAsync();

        var restartedDoctor = await environment.CreateDoctorClientAsync();
        var newPatient = await restartedDoctor.CreatePatientAsync("patient-r06-after-restart");
        var newConsultation = await restartedDoctor.CreateConsultationAsync(newPatient.Id);
        await restartedDoctor.UploadAudioAsync(
            newConsultation.Id,
            SyntheticRecording.Wave("R06-AFTER-RESTART"));

        var transcript = await restartedDoctor.WaitForTranscriptAsync(
            newConsultation.Id,
            TimeSpan.FromSeconds(30));
        using var restartedClinicalKnowledge = await environment.CreateClinicalKnowledgeClientAsync();
        var document = await restartedClinicalKnowledge.WaitForCompletedDocumentAsync(
            newPatient.ExternalPatientId,
            newConsultation.Id.ToString(),
            TimeSpan.FromSeconds(30));
        var persistedDocument = await restartedClinicalKnowledge.WaitForCompletedDocumentAsync(
            existingPatient.ExternalPatientId,
            existingConsultation.Id.ToString(),
            TimeSpan.FromSeconds(5));

        Assert.Equal("Completed", transcript.Status);
        Assert.Equal("Completed", document.Status);
        Assert.Equal("Completed", persistedDocument.Status);
    }

    private static void ScriptSuccessfulTranscriptionAndIngestion(
        AcceptanceEnvironment environment,
        string summary)
    {
        environment.Speech.EnqueueTranscript(
            "Doctor: This is a controlled follow-up.\nPatient: The symptoms are improving.");
        environment.Models.EnqueueTranscriptPlan(
            contextBlurb: "Controlled follow-up with improving symptoms.",
            summary);
    }
}

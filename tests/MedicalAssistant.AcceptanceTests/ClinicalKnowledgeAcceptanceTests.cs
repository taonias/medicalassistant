namespace MedicalAssistant.AcceptanceTests;

public sealed class ClinicalKnowledgeAcceptanceTests
{
    [Fact]
    public async Task Ingestion_status_survives_a_clinical_knowledge_process_restart()
    {
        await using var environment = new AcceptanceEnvironment();
        await environment.StartAsync();
        Guid ingestionId;
        using (var clinicalKnowledge = await environment.CreateClinicalKnowledgeClientAsync())
        {
            ingestionId = await clinicalKnowledge.SubmitTranscriptAsync(
                doctorId: "doctor-t02",
                patientId: "patient-t02",
                sessionId: "consultation-t02",
                transcript: "Doctor: Synthetic acceptance transcript.\nPatient: Synthetic response.");

            var accepted = await clinicalKnowledge.GetIngestionAsync(ingestionId);
            Assert.Equal(ingestionId, accepted.IngestionId);
            Assert.Equal("Queued", accepted.Status);
        }

        await environment.ClinicalKnowledge.StopAsync();
        Assert.False(environment.ClinicalKnowledge.IsRunning);

        await environment.ClinicalKnowledge.StartAsync();
        using var restartedClinicalKnowledge = await environment.CreateClinicalKnowledgeClientAsync();
        var recovered = await restartedClinicalKnowledge.GetIngestionAsync(ingestionId);
        Assert.Equal("Queued", recovered.Status);
    }
}

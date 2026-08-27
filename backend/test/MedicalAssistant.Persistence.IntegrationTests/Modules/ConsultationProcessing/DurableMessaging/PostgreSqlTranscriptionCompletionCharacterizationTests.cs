using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.DurableMessaging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MedicalAssistant.Persistence.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlTranscriptionCompletionCharacterizationTests
{
    private readonly PostgreSqlDatabaseFixture _database;

    public PostgreSqlTranscriptionCompletionCharacterizationTests(PostgreSqlDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Completion_commits_inbox_transcript_consultation_and_outbox_as_one_outcome()
    {
        await _database.ResetAsync();
        await using (var setup = _database.CreateContext())
        {
            setup.Consultations.Add(CreateAudioConsultation());
            await setup.SaveChangesAsync();
        }

        var envelope = CreateEnvelope(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        await using (var context = _database.CreateContext())
        {
            var unitOfWork = new TranscriptionCompletionUnitOfWork(context);
            var result = await unitOfWork.CompleteAsync(
                new TranscriptionCompletionRequest(
                    "transcription-worker",
                    envelope,
                    "observable transcript text",
                    ExternalJobId: "speech-job-atomic",
                    LanguageCode: "en-US"));

            Assert.Equal(TranscriptionCompletionStatus.Completed, result.Status);
        }

        await using var verification = _database.CreateContext();
        var consultation = await verification.Consultations.SingleAsync();
        var inbox = await verification.ConsultationInboxMessages.SingleAsync();
        var transcript = await verification.Transcripts.SingleAsync();
        var outbox = await verification.ConsultationOutboxMessages.SingleAsync();
        Assert.Equal(ConsultationStatus.Transcribed, consultation.Status);
        Assert.Equal(ConsultationEventMessageStatus.Completed, inbox.Status);
        Assert.Equal("observable transcript text", transcript.TranscriptText);
        Assert.Equal(1, transcript.Revision);
        Assert.Equal(ConsultationIntegrationEvents.TranscriptReadyV1, outbox.EventType);
        using var payload = JsonDocument.Parse(outbox.Payload);
        Assert.Equal(transcript.Id, payload.RootElement.GetProperty("transcriptId").GetInt32());
        Assert.DoesNotContain("observable transcript text", outbox.Payload);
    }

    [Fact]
    public async Task Completion_rolls_back_all_state_when_transcript_ready_outbox_insert_fails()
    {
        await _database.ResetAsync();
        await using (var setup = _database.CreateContext())
        {
            setup.Consultations.Add(CreateAudioConsultation());
            await setup.SaveChangesAsync();
            await setup.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "ConsultationOutboxMessages"
                ADD CONSTRAINT "CK_R05_RejectTranscriptReady"
                CHECK ("EventType" <> 'consultation.transcript-ready.v1');
                """);
        }

        await using (var context = _database.CreateContext())
        {
            var unitOfWork = new TranscriptionCompletionUnitOfWork(context);
            await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.CompleteAsync(
                new TranscriptionCompletionRequest(
                    "transcription-worker",
                    CreateEnvelope(Guid.Parse("12121212-1212-1212-1212-121212121212")),
                    "must roll back",
                    ExternalJobId: "speech-job-rollback",
                    LanguageCode: "en-US")));
        }

        await using var verification = _database.CreateContext();
        Assert.Equal(
            ConsultationStatus.AudioUploaded,
            (await verification.Consultations.SingleAsync()).Status);
        Assert.Empty(verification.ConsultationInboxMessages);
        Assert.Empty(verification.Transcripts);
        Assert.Empty(verification.ConsultationOutboxMessages);
    }

    [Fact]
    public async Task Reprocessing_advances_transcript_revision_and_publishes_the_new_revision()
    {
        await _database.ResetAsync();
        var originalConcurrencyToken = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        await using (var setup = _database.CreateContext())
        {
            setup.Consultations.Add(CreateAudioConsultation());
            setup.Transcripts.Add(new Transcript
            {
                ConsultationId = 10,
                Status = TranscriptStatus.Completed,
                TranscriptText = "first transcript",
                ExternalJobId = "speech-job-first",
                Revision = 1,
                ConcurrencyToken = originalConcurrencyToken
            });
            await setup.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var unitOfWork = new TranscriptionCompletionUnitOfWork(context);
            var result = await unitOfWork.CompleteAsync(
                new TranscriptionCompletionRequest(
                    "transcription-worker",
                    CreateEnvelope(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")),
                    "corrected transcript",
                    ExternalJobId: "speech-job-second",
                    LanguageCode: "en-US"));

            Assert.Equal(TranscriptionCompletionStatus.Completed, result.Status);
        }

        await using var verification = _database.CreateContext();
        var transcript = await verification.Transcripts.SingleAsync();
        var outbox = await verification.ConsultationOutboxMessages.SingleAsync();
        Assert.Equal("corrected transcript", transcript.TranscriptText);
        Assert.Equal(2, transcript.Revision);
        Assert.NotEqual(originalConcurrencyToken, transcript.ConcurrencyToken);
        using var payload = JsonDocument.Parse(outbox.Payload);
        Assert.Equal(2, payload.RootElement.GetProperty("transcriptRevision").GetInt32());
    }

    private static Consultation CreateAudioConsultation()
    {
        return new Consultation
        {
            Id = 10,
            DoctorId = "doctor-completion",
            ConsultationDate = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc),
            SourceFileKind = ConsultationFileKind.Audio,
            SourceObjectReference = "private://consultations/10/audio.webm",
            Status = ConsultationStatus.AudioUploaded
        };
    }

    private static IntegrationEventEnvelope<ConsultationAudioUploadedV1> CreateEnvelope(Guid eventId)
    {
        return new IntegrationEventEnvelope<ConsultationAudioUploadedV1>(
            eventId,
            ConsultationIntegrationEvents.AudioUploadedV1,
            1,
            new DateTime(2026, 8, 23, 10, 1, 0, DateTimeKind.Utc),
            "medicalassistant.backend",
            "correlation-completion",
            null,
            new ConsultationAudioUploadedV1(
                10,
                "consultations/10/audio.webm",
                "audio/webm",
                "private://consultations/10/audio.webm",
                42));
    }
}

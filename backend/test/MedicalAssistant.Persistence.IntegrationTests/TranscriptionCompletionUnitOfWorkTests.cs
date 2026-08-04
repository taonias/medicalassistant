using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.DatabaseContext;
using MedicalAssistant.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MedicalAssistant.Persistence.IntegrationTests;

public class TranscriptionCompletionUnitOfWorkTests
{
    [Fact]
    public async Task CompleteAsync_commits_inbox_transcript_consultation_status_and_result_outbox()
    {
        await using var context = CreateContext();
        context.Consultations.Add(new Consultation
        {
            Id = 10,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/10/audio",
            Status = ConsultationStatus.AudioUploaded
        });
        await context.SaveChangesAsync();
        var unitOfWork = new TranscriptionCompletionUnitOfWork(context);
        var envelope = CreateEnvelope(eventId: Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var result = await unitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest(
                "transcription-worker",
                envelope,
                "safe transcript text",
                ExternalJobId: "speech-job-1",
                LanguageCode: "en-US"),
            CancellationToken.None);

        Assert.Equal(TranscriptionCompletionStatus.Completed, result.Status);
        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal(ConsultationEventMessageStatus.Completed, inbox.Status);
        Assert.Equal(envelope.EventId, inbox.EventId);

        var transcript = Assert.Single(context.Transcripts);
        Assert.Equal(10, transcript.ConsultationId);
        Assert.Equal("safe transcript text", transcript.TranscriptText);
        Assert.Equal("speech-job-1", transcript.ExternalJobId);
        Assert.Equal(1, transcript.Revision);
        Assert.Equal(TranscriptStatus.Completed, transcript.Status);

        Assert.Equal(ConsultationStatus.Transcribed, context.Consultations.Single().Status);
        var outbox = Assert.Single(context.ConsultationOutboxMessages);
        Assert.Equal(ConsultationIntegrationEvents.TranscriptReadyV1, outbox.EventType);
        Assert.Contains("\"transcriptId\":", outbox.Payload);
        Assert.DoesNotContain("safe transcript text", outbox.Payload);
    }

    [Fact]
    public async Task CompleteAsync_treats_completed_inbox_event_as_duplicate_noop()
    {
        await using var context = CreateContext();
        context.Consultations.Add(new Consultation
        {
            Id = 10,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/10/audio",
            Status = ConsultationStatus.AudioUploaded
        });
        await context.SaveChangesAsync();
        var unitOfWork = new TranscriptionCompletionUnitOfWork(context);
        var envelope = CreateEnvelope(eventId: Guid.Parse("22222222-2222-2222-2222-222222222222"));

        await unitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest("transcription-worker", envelope, "first text", "job-1", "en-US"),
            CancellationToken.None);
        var duplicate = await unitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest("transcription-worker", envelope, "second text", "job-2", "en-US"),
            CancellationToken.None);

        Assert.Equal(TranscriptionCompletionStatus.DuplicateCompleted, duplicate.Status);
        Assert.Single(context.ConsultationInboxMessages);
        Assert.Single(context.ConsultationOutboxMessages);
        Assert.Equal("first text", Assert.Single(context.Transcripts).TranscriptText);
    }

    [Fact]
    public async Task CompleteAsync_advances_revision_and_concurrency_token_when_replacing_existing_transcript()
    {
        await using var context = CreateContext();
        context.Consultations.Add(new Consultation
        {
            Id = 10,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/10/audio",
            Status = ConsultationStatus.AudioUploaded
        });
        var originalToken = Guid.Parse("99999999-9999-9999-9999-999999999999");
        context.Transcripts.Add(new Transcript
        {
            ConsultationId = 10,
            Status = TranscriptStatus.Completed,
            TranscriptText = "old transcript",
            Revision = 1,
            ConcurrencyToken = originalToken
        });
        await context.SaveChangesAsync();
        var unitOfWork = new TranscriptionCompletionUnitOfWork(context);

        var result = await unitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest(
                "transcription-worker",
                CreateEnvelope(Guid.Parse("99999999-1111-1111-1111-999999999999")),
                "new transcript",
                ExternalJobId: "speech-job-new",
                LanguageCode: "en-US"),
            CancellationToken.None);

        Assert.Equal(TranscriptionCompletionStatus.Completed, result.Status);
        var transcript = Assert.Single(context.Transcripts);
        Assert.Equal("new transcript", transcript.TranscriptText);
        Assert.Equal(2, transcript.Revision);
        Assert.NotEqual(originalToken, transcript.ConcurrencyToken);
        Assert.Contains("\"transcriptRevision\":2", Assert.Single(context.ConsultationOutboxMessages).Payload);
    }

    [Fact]
    public async Task FailAsync_commits_failed_transcript_consultation_inbox_and_failure_outbox()
    {
        await using var context = CreateContext();
        context.Consultations.Add(new Consultation
        {
            Id = 10,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/10/audio",
            Status = ConsultationStatus.AudioUploaded
        });
        await context.SaveChangesAsync();
        var unitOfWork = new TranscriptionCompletionUnitOfWork(context);
        var envelope = CreateEnvelope(eventId: Guid.Parse("66666666-6666-6666-6666-666666666666"));

        var result = await unitOfWork.FailAsync(
            new TranscriptionFailureRequest(
                "transcription-worker",
                envelope,
                "speech-unsupported-audio",
                "Permanent"),
            CancellationToken.None);

        Assert.Equal(TranscriptionFailureStatus.Failed, result.Status);
        Assert.Equal(ConsultationStatus.Failed, context.Consultations.Single().Status);
        Assert.Equal("speech-unsupported-audio", context.Consultations.Single().FailureReason);
        Assert.Equal(TranscriptStatus.Failed, context.Transcripts.Single().Status);
        Assert.Equal("speech-unsupported-audio", context.Transcripts.Single().FailureReason);
        Assert.Equal(ConsultationEventMessageStatus.Completed, context.ConsultationInboxMessages.Single().Status);
        Assert.Equal("Permanent", context.ConsultationInboxMessages.Single().LastFailureCategory);
        Assert.Equal("speech-unsupported-audio", context.ConsultationInboxMessages.Single().LastFailureCode);
        var outbox = Assert.Single(context.ConsultationOutboxMessages);
        Assert.Equal(ConsultationIntegrationEvents.TranscriptionFailedV1, outbox.EventType);
        Assert.Contains("\"failureCode\":\"speech-unsupported-audio\"", outbox.Payload);
        Assert.DoesNotContain("private://", outbox.Payload);
    }

    [Fact]
    public async Task CompleteAsync_marks_inbox_completed_but_ignores_deleted_consultation()
    {
        await using var context = CreateContext();
        var consultation = new Consultation
        {
            Id = 10,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/10/audio",
            Status = ConsultationStatus.AudioUploaded
        };
        consultation.MarkDeleted("doctor-1", "doctor-delete");
        context.Consultations.Add(consultation);
        await context.SaveChangesAsync();
        var unitOfWork = new TranscriptionCompletionUnitOfWork(context);

        var result = await unitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest(
                "transcription-worker",
                CreateEnvelope(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                "late transcript text",
                ExternalJobId: "speech-job-late",
                LanguageCode: "en-US"),
            CancellationToken.None);

        Assert.Equal(TranscriptionCompletionStatus.IgnoredDeleted, result.Status);
        Assert.Empty(context.Transcripts);
        Assert.Empty(context.ConsultationOutboxMessages);
        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal(ConsultationEventMessageStatus.Completed, inbox.Status);
        Assert.Equal("StateGate", inbox.LastFailureCategory);
        Assert.Equal("consultation-deleted", inbox.LastFailureCode);
        Assert.Equal(ConsultationStatus.Deleted, context.Consultations.Single().Status);
    }

    [Fact]
    public async Task CompleteAsync_marks_inbox_completed_but_ignores_superseded_source()
    {
        await using var context = CreateContext();
        context.Consultations.Add(new Consultation
        {
            Id = 10,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/10/new-audio",
            Status = ConsultationStatus.AudioUploaded
        });
        await context.SaveChangesAsync();
        var unitOfWork = new TranscriptionCompletionUnitOfWork(context);

        var result = await unitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest(
                "transcription-worker",
                CreateEnvelope(Guid.Parse("88888888-8888-8888-8888-888888888888")),
                "late transcript text",
                ExternalJobId: "speech-job-late",
                LanguageCode: "en-US"),
            CancellationToken.None);

        Assert.Equal(TranscriptionCompletionStatus.IgnoredSuperseded, result.Status);
        Assert.Empty(context.Transcripts);
        Assert.Empty(context.ConsultationOutboxMessages);
        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal(ConsultationEventMessageStatus.Completed, inbox.Status);
        Assert.Equal("StateGate", inbox.LastFailureCategory);
        Assert.Equal("consultation-source-superseded", inbox.LastFailureCode);
        Assert.Equal(ConsultationStatus.AudioUploaded, context.Consultations.Single().Status);
    }

    private static MedicalAssistantDatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new MedicalAssistantDatabaseContext(options, new HttpContextAccessor());
    }

    private static IntegrationEventEnvelope<ConsultationAudioUploadedV1> CreateEnvelope(Guid eventId)
    {
        return new IntegrationEventEnvelope<ConsultationAudioUploadedV1>(
            eventId,
            ConsultationIntegrationEvents.AudioUploadedV1,
            1,
            DateTime.UtcNow,
            "medicalassistant.backend",
            "correlation-1",
            null,
            new ConsultationAudioUploadedV1(
                10,
                "file-10",
                "audio/wav",
                "private://consultations/10/audio",
                12));
    }
}

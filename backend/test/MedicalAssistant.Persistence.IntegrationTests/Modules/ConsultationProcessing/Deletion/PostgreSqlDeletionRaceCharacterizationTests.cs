using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.Modules.CareWorkflow.Consultations;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.Deletion;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.TranscriptIngestion;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MedicalAssistant.Persistence.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlDeletionRaceCharacterizationTests
{
    private readonly PostgreSqlDatabaseFixture _database;

    public PostgreSqlDeletionRaceCharacterizationTests(PostgreSqlDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Known_risk_K02_transcript_ready_acceptance_can_reactivate_a_tombstoned_consultation()
    {
        await _database.ResetAsync();
        await using (var setup = _database.CreateContext())
        {
            setup.Consultations.Add(new Consultation
            {
                Id = 20,
                DoctorId = "doctor-deletion-race",
                ConsultationDate = new DateTime(2026, 8, 23, 11, 0, 0, DateTimeKind.Utc),
                SourceFileKind = ConsultationFileKind.Audio,
                SourceObjectReference = "private://consultations/20/audio.webm",
                Status = ConsultationStatus.Transcribed
            });
            await setup.SaveChangesAsync();
        }

        var pause = new PauseBeforeAcceptedConsultationSaveInterceptor();
        await using var acceptanceContext = _database.CreateContext(pause);
        var acceptanceStore = new TranscriptReadyPreparationStore(acceptanceContext);
        var envelope = CreateTranscriptReadyEnvelope();
        var acceptance = acceptanceStore.CompleteAcceptedAsync(
            "backend-clinical-knowledge",
            envelope,
            new TranscriptReadyAcceptedResult(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "doctor-deletion-race#patient-20#20#1",
                Duplicate: false));

        await pause.WaitUntilPausedAsync();
        try
        {
            await using var deletionContext = _database.CreateContext();
            var consultation = await deletionContext.Consultations.SingleAsync();
            consultation.MarkDeleted("doctor-deletion-race", "doctor-delete");
            var deletedOutbox = ConsultationOutboxFactory.Deleted(
                consultation,
                "correlation-deletion-race");
            var cleanup = new ConsultationDeletionCleanup
            {
                ConsultationId = consultation.Id,
                DeletionEventId = deletedOutbox.EventId,
                DeletedAtUtc = consultation.DeletedAtUtc!.Value
            };
            var repository = new ConsultationRepository(
                deletionContext,
                new HttpContextAccessor());
            await repository.RecordDeletionAsync(consultation, cleanup, deletedOutbox);
        }
        finally
        {
            pause.Release();
        }

        await acceptance;

        await using var verification = _database.CreateContext();
        var persistedConsultation = await verification.Consultations.SingleAsync();
        Assert.NotNull(persistedConsultation.DeletedAtUtc);
        Assert.Equal(ConsultationStatus.StructuredDataPending, persistedConsultation.Status);
        Assert.Equal(
            ConsultationEventMessageStatus.Completed,
            (await verification.ConsultationInboxMessages.SingleAsync()).Status);
        Assert.Equal(
            ConsultationIntegrationEvents.DeletedV1,
            (await verification.ConsultationOutboxMessages.SingleAsync()).EventType);
        Assert.Single(verification.ConsultationDeletionCleanups);
    }

    private static IntegrationEventEnvelope<ConsultationTranscriptReadyV1> CreateTranscriptReadyEnvelope()
    {
        return new IntegrationEventEnvelope<ConsultationTranscriptReadyV1>(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ConsultationIntegrationEvents.TranscriptReadyV1,
            1,
            new DateTime(2026, 8, 23, 11, 1, 0, DateTimeKind.Utc),
            "medicalassistant.transcription-worker",
            "correlation-transcript-ready",
            null,
            new ConsultationTranscriptReadyV1(
                20,
                "private://consultations/20/audio.webm",
                200,
                1,
                "en-US"));
    }

    private sealed class PauseBeforeAcceptedConsultationSaveInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitUntilPausedAsync() => _paused.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Release() => _released.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (IsAcceptedConsultationSave(eventData.Context))
            {
                _paused.TrySetResult();
                await _released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }

        private static bool IsAcceptedConsultationSave(DbContext? context)
        {
            return context?.ChangeTracker.Entries<Consultation>().Any(IsAcceptedConsultation) == true;
        }

        private static bool IsAcceptedConsultation(EntityEntry<Consultation> entry)
        {
            return entry.State == EntityState.Modified &&
                   entry.Entity.Status == ConsultationStatus.StructuredDataPending;
        }
    }
}

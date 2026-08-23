using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlRecordingOutboxCharacterizationTests
{
    private readonly PostgreSqlDatabaseFixture _database;

    public PostgreSqlRecordingOutboxCharacterizationTests(PostgreSqlDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Recording_registration_commits_consultation_state_and_outbox_fact_together()
    {
        await _database.ResetAsync();
        await using var context = _database.CreateContext();
        var consultation = new Consultation
        {
            DoctorId = "doctor-atomicity",
            ConsultationDate = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc),
            Status = ConsultationStatus.Draft
        };
        context.Consultations.Add(consultation);
        await context.SaveChangesAsync();

        consultation.MarkAudioUploaded(
            "private://consultations/atomicity/audio.webm",
            "audio/webm",
            durationSeconds: 42);
        var outbox = ConsultationOutboxFactory.AudioUploaded(
            consultation,
            "consultations/atomicity/audio.webm",
            "correlation-atomicity");
        var repository = new ConsultationRepository(context, new HttpContextAccessor());

        await repository.UpdateWithOutboxAsync(consultation, outbox);

        await using var verification = _database.CreateContext();
        var savedConsultation = await verification.Consultations.SingleAsync();
        var savedOutbox = await verification.ConsultationOutboxMessages.SingleAsync();
        Assert.Equal(ConsultationStatus.AudioUploaded, savedConsultation.Status);
        Assert.Equal(
            "private://consultations/atomicity/audio.webm",
            savedConsultation.SourceObjectReference);
        Assert.Equal(ConsultationIntegrationEvents.AudioUploadedV1, savedOutbox.EventType);
        Assert.Equal(savedConsultation.Id.ToString(), savedOutbox.AggregateId);
    }

    [Fact]
    public async Task Recording_registration_rolls_back_consultation_state_when_outbox_insert_fails()
    {
        await _database.ResetAsync();
        var duplicateEventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        int consultationId;
        await using (var setup = _database.CreateContext())
        {
            var consultation = new Consultation
            {
                DoctorId = "doctor-rollback",
                ConsultationDate = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc),
                Status = ConsultationStatus.Draft
            };
            setup.Consultations.Add(consultation);
            setup.ConsultationOutboxMessages.Add(CreateExistingOutbox(duplicateEventId));
            await setup.SaveChangesAsync();
            consultationId = consultation.Id;
        }

        await using (var context = _database.CreateContext())
        {
            var consultation = await context.Consultations.SingleAsync();
            consultation.MarkAudioUploaded(
                "private://consultations/rollback/audio.webm",
                "audio/webm",
                durationSeconds: 15);
            var duplicateOutbox = ConsultationOutboxFactory.AudioUploaded(
                consultation,
                "consultations/rollback/audio.webm",
                "correlation-rollback");
            duplicateOutbox.EventId = duplicateEventId;
            var repository = new ConsultationRepository(context, new HttpContextAccessor());

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                repository.UpdateWithOutboxAsync(consultation, duplicateOutbox));
        }

        await using var verification = _database.CreateContext();
        var savedConsultation = await verification.Consultations.SingleAsync();
        Assert.Equal(consultationId, savedConsultation.Id);
        Assert.Equal(ConsultationStatus.Draft, savedConsultation.Status);
        Assert.Null(savedConsultation.SourceObjectReference);
        Assert.Single(verification.ConsultationOutboxMessages);
    }

    private static ConsultationOutboxMessage CreateExistingOutbox(Guid eventId)
    {
        return new ConsultationOutboxMessage
        {
            EventId = eventId,
            EventType = ConsultationIntegrationEvents.AudioUploadedV1,
            EventVersion = 1,
            OccurredAtUtc = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc),
            Producer = "medicalassistant.backend",
            CorrelationId = "existing-correlation",
            AggregateType = nameof(Consultation),
            AggregateId = "existing",
            Payload = "{}",
            CreatedAtUtc = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc)
        };
    }
}

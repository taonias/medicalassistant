using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public sealed class ConsultationOutboxStore : IConsultationOutboxStore
{
    private readonly MedicalAssistantDatabaseContext _context;

    public ConsultationOutboxStore(MedicalAssistantDatabaseContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ConsultationOutboxMessage>> ClaimDueAsync(
        int batchSize,
        string leaseOwner,
        TimeSpan leaseDuration,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        var messages = await _context.ConsultationOutboxMessages
            .FromSqlInterpolated($"""
                UPDATE "ConsultationOutboxMessages"
                SET "Status" = 1,
                    "LeaseOwner" = {leaseOwner},
                    "LeaseExpiresAtUtc" = {leaseExpiresAtUtc}
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM "ConsultationOutboxMessages"
                    WHERE "Status" IN (0, 3)
                      AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {nowUtc})
                      AND ("LeaseExpiresAtUtc" IS NULL OR "LeaseExpiresAtUtc" <= {nowUtc})
                    ORDER BY "CreatedAtUtc", "Id"
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *
                """)
            .ToListAsync(cancellationToken);

        return messages
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .ToList();
    }

    public async Task MarkPublishedAsync(
        long messageId,
        DateTime publishedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.ConsultationOutboxMessages
            .FirstAsync(message => message.Id == messageId, cancellationToken);
        message.Status = ConsultationEventMessageStatus.Completed;
        message.PublishedAtUtc = publishedAtUtc;
        message.LeaseOwner = null;
        message.LeaseExpiresAtUtc = null;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        long messageId,
        string failureCategory,
        string failureCode,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.ConsultationOutboxMessages
            .FirstAsync(message => message.Id == messageId, cancellationToken);
        message.Status = ConsultationEventMessageStatus.Failed;
        message.AttemptCount++;
        message.NextAttemptAtUtc = nextAttemptAtUtc;
        message.LeaseOwner = null;
        message.LeaseExpiresAtUtc = null;
        message.LastFailureCategory = failureCategory;
        message.LastFailureCode = failureCode;
        await _context.SaveChangesAsync(cancellationToken);
    }
}

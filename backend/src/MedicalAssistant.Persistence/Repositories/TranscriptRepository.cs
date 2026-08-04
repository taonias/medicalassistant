using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class TranscriptRepository : GenericRepository<Transcript>, ITranscriptRepository
{
    public TranscriptRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<Transcript?> GetByConsultationIdAsync(int consultationId)
    {
        return await _context.Transcripts
            .FirstOrDefaultAsync(t => t.ConsultationId == consultationId);
    }

    public async Task<Transcript?> GetByExternalJobIdAsync(string externalJobId)
    {
        return await _context.Transcripts
            .FirstOrDefaultAsync(t => t.ExternalJobId == externalJobId);
    }

    public async Task<Transcript> UpdateWithOutboxAsync(
        Transcript transcript,
        Consultation consultation,
        ConsultationOutboxMessage outboxMessage,
        CancellationToken cancellationToken = default)
    {
        _context.Entry(transcript).State = EntityState.Modified;
        _context.Entry(consultation).State = EntityState.Modified;
        await _context.ConsultationOutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return transcript;
    }

    public async Task<Transcript> CompleteWithOutboxAsync(
        Transcript transcript,
        Consultation consultation,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (transcript.Id == 0)
        {
            await _context.Transcripts.AddAsync(transcript, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _context.Entry(transcript).State = EntityState.Modified;
        }

        _context.Entry(consultation).State = EntityState.Modified;
        var outboxMessage = ConsultationOutboxFactory.TranscriptReady(consultation, transcript, correlationId);
        await _context.ConsultationOutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return transcript;
    }
}

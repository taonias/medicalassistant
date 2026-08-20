using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class ConversationRepository : GenericRepository<Conversation>, IConversationRepository
{
    public ConversationRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<Conversation?> GetForDoctorAsync(int id, string doctorId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == id && c.DoctorId == doctorId, cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> ListByPatientForDoctorAsync(
        int patientId, string doctorId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .AsNoTracking()
            .Where(c => c.PatientId == patientId
                        && c.DoctorId == doctorId
                        && c.Status == ConversationStatus.Active)
            .OrderByDescending(c => c.DateModified ?? c.DateCreated)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesForDoctorAsync(
        int conversationId, string doctorId, CancellationToken cancellationToken = default)
    {
        var owned = await _context.Conversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == conversationId && c.DoctorId == doctorId, cancellationToken);
        if (!owned)
        {
            return [];
        }

        return await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Citations)
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(
        int conversationId, int beforeSequence, int take, CancellationToken cancellationToken = default)
    {
        // Take the most-recent terminal turns below the current turn, then flip back to
        // chronological order so the AI reads them oldest-first.
        var recent = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId
                        && m.Sequence < beforeSequence
                        && (m.State == MessageState.Completed
                            || m.State == MessageState.Refused))
            .OrderByDescending(m => m.Sequence)
            .Take(take)
            .ToListAsync(cancellationToken);

        return recent.OrderBy(m => m.Sequence).ToList();
    }

    public async Task<ChatMessage?> GetAssistantByAskIdForDoctorAsync(
        Guid askId, string doctorId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Citations)
            .Where(m => m.AskId == askId && m.Role == MessageRole.Assistant)
            .Where(m => _context.Conversations.Any(c => c.Id == m.ConversationId && c.DoctorId == doctorId))
            .OrderByDescending(m => m.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ChatMessage?> GetTrackedMessageForDoctorAsync(
        int conversationId, int messageId, string doctorId, CancellationToken cancellationToken = default)
    {
        var owned = await _context.Conversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == conversationId && c.DoctorId == doctorId, cancellationToken);
        if (!owned)
        {
            return null;
        }

        return await _context.ChatMessages
            .Include(m => m.Citations)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId, cancellationToken);
    }

    public async Task<ChatMessage?> GetUserMessageByAskIdAsync(
        int conversationId, Guid askId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.ConversationId == conversationId
                     && m.AskId == askId
                     && m.Role == MessageRole.User,
                cancellationToken);
    }

    public async Task<int> GetNextSequenceAsync(int conversationId, CancellationToken cancellationToken = default)
    {
        var max = await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .Select(m => (int?)m.Sequence)
            .MaxAsync(cancellationToken);

        return (max ?? 0) + 1;
    }

    public async Task<Conversation?> GetTrackedByIdAsync(int conversationId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesForSummaryAsync(
        int conversationId, int afterSequence, int throughSequence, CancellationToken cancellationToken = default)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId
                        && m.Sequence > afterSequence
                        && m.Sequence <= throughSequence
                        && (m.State == MessageState.Completed
                            || m.State == MessageState.Refused))
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken);
    }

    public async Task AddMessagesAsync(CancellationToken cancellationToken, params ChatMessage[] messages)
    {
        await _context.ChatMessages.AddRangeAsync(messages, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

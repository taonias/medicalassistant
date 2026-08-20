using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IConversationRepository : IGenericRepository<Conversation>
{
    /// <summary>The conversation (tracked, for mutation) if it exists and belongs to the doctor.</summary>
    Task<Conversation?> GetForDoctorAsync(int id, string doctorId, CancellationToken cancellationToken = default);

    /// <summary>Active conversations about a patient, most recently updated first (history list).</summary>
    Task<IReadOnlyList<Conversation>> ListByPatientForDoctorAsync(
        int patientId, string doctorId, CancellationToken cancellationToken = default);

    /// <summary>All messages of a conversation (with citations), ordered by sequence — rehydrates a thread.</summary>
    Task<IReadOnlyList<ChatMessage>> GetMessagesForDoctorAsync(
        int conversationId, string doctorId, CancellationToken cancellationToken = default);

    /// <summary>The last <paramref name="take"/> terminal messages before <paramref name="beforeSequence"/>, oldest-first — the verbatim context window.</summary>
    Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(
        int conversationId, int beforeSequence, int take, CancellationToken cancellationToken = default);

    /// <summary>An existing assistant turn for this askId (with citations), for idempotency; null if none.</summary>
    Task<ChatMessage?> GetAssistantByAskIdForDoctorAsync(
        Guid askId, string doctorId, CancellationToken cancellationToken = default);

    /// <summary>A tracked assistant message (with citations) for a doctor-triggered retry.</summary>
    Task<ChatMessage?> GetTrackedMessageForDoctorAsync(
        int conversationId, int messageId, string doctorId, CancellationToken cancellationToken = default);

    /// <summary>The user question paired with a turn (same askId), for re-running on retry.</summary>
    Task<ChatMessage?> GetUserMessageByAskIdAsync(
        int conversationId, Guid askId, CancellationToken cancellationToken = default);

    /// <summary>The next 1-based sequence number for a conversation.</summary>
    Task<int> GetNextSequenceAsync(int conversationId, CancellationToken cancellationToken = default);

    /// <summary>A tracked conversation by id, doctor-agnostic — for background summary refresh.</summary>
    Task<Conversation?> GetTrackedByIdAsync(int conversationId, CancellationToken cancellationToken = default);

    /// <summary>Terminal turns in (afterSequence, throughSequence], oldest-first — the batch to fold into the summary.</summary>
    Task<IReadOnlyList<ChatMessage>> GetMessagesForSummaryAsync(
        int conversationId, int afterSequence, int throughSequence, CancellationToken cancellationToken = default);

    /// <summary>Adds messages to the store in one save (e.g. the user turn + the pending assistant turn).</summary>
    Task AddMessagesAsync(CancellationToken cancellationToken, params ChatMessage[] messages);

    /// <summary>Commits mutations already made to entities tracked by this unit of work.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

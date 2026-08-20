using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Application.Features.Chat.Common;

/// <summary>The result of asking a turn: the assistant message, delivered whole (never streamed).</summary>
public sealed class AskChatResponse
{
    public int ConversationId { get; set; }
    public required string Title { get; set; }
    public int MessageId { get; set; }
    public MessageState State { get; set; }
    public required string Answer { get; set; }
    public string? Language { get; set; }
    public bool Refused { get; set; }
    public string? FailureReason { get; set; }
    public Guid AskId { get; set; }
    public List<ChatCitationDto> Citations { get; set; } = [];
}

/// <summary>One cited Evidence Item, carried structurally to the client for interactive rendering.</summary>
public sealed class ChatCitationDto
{
    public required string Label { get; set; }
    public Guid ChunkId { get; set; }
    public required string DocumentId { get; set; }
    public required string DocumentType { get; set; }
    public string? SessionId { get; set; }
    public DateTimeOffset? DocumentDate { get; set; }
    public string? SourceRef { get; set; }
    public required string Quote { get; set; }
    public double Score { get; set; }
}

/// <summary>A conversation header for the history list.</summary>
public sealed class ConversationSummaryDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public int PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public ConversationStatus Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>A full conversation thread (header + messages) for rehydration.</summary>
public sealed class ConversationThreadDto
{
    public required ConversationSummaryDto Conversation { get; set; }
    public List<ConversationMessageDto> Messages { get; set; } = [];
}

public sealed class ConversationMessageDto
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public MessageRole Role { get; set; }
    public required string Content { get; set; }
    public MessageState State { get; set; }
    public string? Language { get; set; }
    public string? FailureReason { get; set; }
    public Guid AskId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<ChatCitationDto> Citations { get; set; } = [];
}

/// <summary>Manual mapping between chat entities and their transport DTOs.</summary>
public static class ChatMapping
{
    public static ChatCitationDto ToDto(this MessageCitation c) => new()
    {
        Label = c.Label,
        ChunkId = c.ChunkId,
        DocumentId = c.DocumentId,
        DocumentType = c.DocumentType,
        SessionId = c.SessionId,
        DocumentDate = c.DocumentDate,
        SourceRef = c.SourceRef,
        Quote = c.Quote,
        Score = c.Score,
    };

    public static ConversationSummaryDto ToSummaryDto(this Conversation c) => new()
    {
        Id = c.Id,
        Title = c.Title,
        PatientId = c.PatientId,
        ConsultationId = c.ConsultationId,
        Status = c.Status,
        CreatedAt = c.DateCreated,
        UpdatedAt = c.DateModified,
    };

    public static ConversationMessageDto ToDto(this ChatMessage m) => new()
    {
        Id = m.Id,
        Sequence = m.Sequence,
        Role = m.Role,
        Content = m.Content,
        State = m.State,
        Language = m.Language,
        FailureReason = m.FailureReason,
        AskId = m.AskId,
        CreatedAt = m.DateCreated,
        Citations = m.Citations.Select(c => c.ToDto()).ToList(),
    };

    public static AskChatResponse ToAskResponse(this ChatMessage assistant, Conversation conversation) => new()
    {
        ConversationId = conversation.Id,
        Title = conversation.Title,
        MessageId = assistant.Id,
        State = assistant.State,
        Answer = assistant.Content,
        Language = assistant.Language,
        Refused = assistant.State == MessageState.Refused,
        FailureReason = assistant.FailureReason,
        AskId = assistant.AskId,
        Citations = assistant.Citations.Select(c => c.ToDto()).ToList(),
    };
}

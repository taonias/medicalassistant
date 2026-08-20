using System.Globalization;
using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Features.Chat.Common;

/// <summary>
/// The identifier the AI service knows a patient by: the external id when present,
/// otherwise the internal id. Must match what was used at ingestion
/// (see ConsultationTranscriptReadyIntegrationEventHandler).
/// </summary>
public static class ClinicalPatientId
{
    public static string For(Domain.Patient patient) =>
        string.IsNullOrWhiteSpace(patient.ExternalPatientId)
            ? patient.Id.ToString(CultureInfo.InvariantCulture)
            : patient.ExternalPatientId;
}

/// <summary>Auto-titles a conversation from its first question (deterministic; no model call).</summary>
public static class ConversationTitle
{
    private const int MaxLength = 60;
    private const string Fallback = "New conversation";

    public static string FromQuestion(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return Fallback;
        }

        // First line, collapsed whitespace.
        var firstLine = question.Replace('\r', ' ').Replace('\n', ' ').Trim();
        firstLine = string.Join(' ', firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (firstLine.Length <= MaxLength)
        {
            return firstLine;
        }

        // Cut at the last word boundary within the limit, then ellipsize.
        var cut = firstLine[..MaxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > MaxLength / 2)
        {
            cut = cut[..lastSpace];
        }

        return cut.TrimEnd() + "…";
    }
}

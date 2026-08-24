using System.Text;
using MedicalAssistance.Ingestion.Api.Ingestions;
using Microsoft.Extensions.AI;

namespace MedicalAssistance.Ingestion.Api.Chat;

/// <summary>One older turn to fold into the rolling conversation summary.</summary>
public sealed record ConversationTurnInput(string Role, string Text);

/// <summary>
/// Folds older doctor↔AI turns into a single rolling conversation summary (ADR-0008
/// agent, DB-owned instructions). Stateless and pure: prior summary + new turns in,
/// updated summary out. The backend owns when this runs and where the result is stored;
/// the summary is only ever phrasing/refinement context, never evidence.
/// </summary>
public interface IConversationSummarizer
{
    Task<string> SummarizeAsync(
        string? priorSummary, IReadOnlyList<ConversationTurnInput> newTurns, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class ConversationSummarizer(
    IChatClient chatClient, AgentInstructionProvider instructionProvider) : IConversationSummarizer
{
    public async Task<string> SummarizeAsync(
        string? priorSummary, IReadOnlyList<ConversationTurnInput> newTurns, CancellationToken cancellationToken)
    {
        var (instructions, _) = instructionProvider.Get(AgentNames.ConversationSummarizer);
        var agent = chatClient.AsAIAgent(name: AgentNames.ConversationSummarizer, instructions: instructions);
        var response = await agent.RunAsync(BuildPrompt(priorSummary, newTurns), cancellationToken: cancellationToken);
        return response.Text.Trim();
    }

    private static string BuildPrompt(string? priorSummary, IReadOnlyList<ConversationTurnInput> newTurns)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(priorSummary))
        {
            builder.AppendLine("Existing summary of the earlier conversation:");
            builder.AppendLine(priorSummary);
            builder.AppendLine();
        }

        builder.AppendLine("New turns to fold into the summary (oldest first):");
        foreach (var turn in newTurns)
        {
            builder.Append(turn.Role).Append(": ").AppendLine(turn.Text);
        }

        return builder.ToString();
    }
}

using MedicalAssistance.Ingestion.Api.Chat;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistance.Ingestion.Api.Controllers;

/// <summary>
/// The grounded-chat surface: the one public endpoint of the retrieval feature.
/// Secret-authenticated by the fallback policy like every other endpoint, and
/// stateless — it stores no conversation (ADR-0010). The patient in the route is
/// the hard boundary every retrieval is scoped to.
/// </summary>
[ApiController]
[Route("patients")]
[Produces("application/json")]
public sealed class ChatController(
    IGroundedAnswerService answers, IConversationSummarizer summarizer) : ControllerBase
{
    /// <summary>Answers a question about one patient, grounded in that patient's own record.</summary>
    /// <remarks>
    /// Runs a patient-scoped retrieval over the stored chunks and returns a cited
    /// answer — or, once the safety net lands, an honest insufficient-evidence
    /// refusal. Conversation context (<c>recentTurns</c>, <c>priorSummary</c>) is
    /// accepted as input to interpret the question, but nothing is stored: the same
    /// inputs always produce the same answer.
    /// </remarks>
    /// <param name="patientId">The patient the question is about — the retrieval boundary.</param>
    /// <param name="request">The question, the asking doctor, and optional narrowing and context.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <response code="200">A grounded answer with its citations, or an insufficient-evidence refusal.</response>
    /// <response code="400">The question was missing or blank.</response>
    /// <response code="401">No valid secret was presented.</response>
    /// <response code="500">The generated answer failed citation verification — the turn is failed, not retried (ADR-0012).</response>
    [HttpPost("{patientId}/chat/answer")]
    [ProducesResponseType<ChatAnswerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Answer(
        string patientId, [FromBody] ChatAnswerRequest? request, CancellationToken ct)
    {
        // Minimal gate for T42: a question is required. The full field-level contract
        // (topK range, malformed filters, garbled question) is T48.
        if (request is null || string.IsNullOrWhiteSpace(request.Question))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["question"] = ["A question is required."],
            }));
        }

        try
        {
            return Ok(await answers.AnswerAsync(patientId, request, ct));
        }
        catch (CitationVerificationException)
        {
            // Grounding failed verification: fail the turn, no corrective retry, and
            // never emit the unverified answer (ADR-0012). The response carries no
            // answer text — only a generic 5xx.
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "The generated answer failed grounding verification.");
        }
    }

    /// <summary>
    /// Folds older conversation turns into one rolling summary. Stateless — the backend
    /// owns the conversation state and decides when to call this (window-aligned refresh).
    /// With no turns to fold, the prior summary is returned unchanged.
    /// </summary>
    /// <response code="200">The updated rolling summary.</response>
    /// <response code="401">No valid secret was presented.</response>
    [HttpPost("{patientId}/chat/summarize")]
    [ProducesResponseType<ChatSummarizeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Summarize(
        string patientId, [FromBody] ChatSummarizeRequest? request, CancellationToken ct)
    {
        var turns = (request?.NewTurns ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Select(t => new ConversationTurnInput(t.Role ?? "user", t.Text!))
            .ToList();

        if (turns.Count == 0)
        {
            return Ok(new ChatSummarizeResponse { Summary = request?.PriorSummary ?? string.Empty });
        }

        var summary = await summarizer.SummarizeAsync(request?.PriorSummary, turns, ct);
        return Ok(new ChatSummarizeResponse { Summary = summary });
    }
}

/// <summary>A summarize request: the prior rolling summary and the older turns to fold in.</summary>
public sealed record ChatSummarizeRequest
{
    public string? PriorSummary { get; init; }
    public IReadOnlyList<ChatTurn>? NewTurns { get; init; }
}

/// <summary>The updated rolling conversation summary.</summary>
public sealed record ChatSummarizeResponse
{
    public required string Summary { get; init; }
}

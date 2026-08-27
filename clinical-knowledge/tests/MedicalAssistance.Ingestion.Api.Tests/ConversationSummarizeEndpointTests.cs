using MedicalAssistance.Ingestion.Api.GroundedChat;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistance.Ingestion.Api.Tests;

/// <summary>
/// The conversation-summarize endpoint contract, tested at the controller directly (no DB /
/// no model), since the decision worth locking is deterministic: with nothing to fold the
/// prior summary is returned unchanged and the model is never called; with turns present the
/// summarizer is delegated to.
/// </summary>
public class ConversationSummarizeEndpointTests
{
    [Fact]
    public async Task With_no_turns_it_returns_the_prior_summary_and_never_calls_the_model()
    {
        var summarizer = new RecordingSummarizer("should-not-be-used");
        var controller = new ChatController(new UnusedAnswers(), summarizer);

        var result = await controller.Summarize(
            "patient-1",
            new ChatSummarizeRequest { PriorSummary = "existing summary", NewTurns = [] },
            CancellationToken.None);

        var body = Assert.IsType<ChatSummarizeResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("existing summary", body.Summary);
        Assert.False(summarizer.Called);
    }

    [Fact]
    public async Task Blank_turns_are_dropped_so_an_all_blank_batch_is_still_a_no_op()
    {
        var summarizer = new RecordingSummarizer("nope");
        var controller = new ChatController(new UnusedAnswers(), summarizer);

        var result = await controller.Summarize(
            "patient-1",
            new ChatSummarizeRequest
            {
                PriorSummary = "keep me",
                NewTurns = [new ChatTurn { Role = "user", Text = "   " }],
            },
            CancellationToken.None);

        var body = Assert.IsType<ChatSummarizeResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("keep me", body.Summary);
        Assert.False(summarizer.Called);
    }

    [Fact]
    public async Task With_real_turns_it_delegates_to_the_summarizer()
    {
        var summarizer = new RecordingSummarizer("folded summary");
        var controller = new ChatController(new UnusedAnswers(), summarizer);

        var result = await controller.Summarize(
            "patient-1",
            new ChatSummarizeRequest
            {
                PriorSummary = "old",
                NewTurns = [new ChatTurn { Role = "user", Text = "Any allergies?" }],
            },
            CancellationToken.None);

        var body = Assert.IsType<ChatSummarizeResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("folded summary", body.Summary);
        Assert.True(summarizer.Called);
        Assert.Equal("old", summarizer.SeenPriorSummary);
        Assert.Single(summarizer.SeenTurns);
    }

    private sealed class RecordingSummarizer(string result) : IConversationSummarizer
    {
        public bool Called { get; private set; }
        public string? SeenPriorSummary { get; private set; }
        public IReadOnlyList<ConversationTurnInput> SeenTurns { get; private set; } = [];

        public Task<string> SummarizeAsync(
            string? priorSummary, IReadOnlyList<ConversationTurnInput> newTurns, CancellationToken cancellationToken)
        {
            Called = true;
            SeenPriorSummary = priorSummary;
            SeenTurns = newTurns;
            return Task.FromResult(result);
        }
    }

    private sealed class UnusedAnswers : IGroundedAnswerService
    {
        public Task<ChatAnswerResponse> AnswerAsync(
            string patientId, ChatAnswerRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}

using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Shouldly;

namespace MedicalAssistant.Application.UnitTests.Chat;

public class ChatMessageStateTests
{
    [Fact]
    public void A_new_assistant_message_starts_pending_and_empty()
    {
        var message = new ChatMessage { Role = MessageRole.Assistant };

        message.State.ShouldBe(MessageState.Pending);
        message.Content.ShouldBe(string.Empty);
        message.Citations.ShouldBeEmpty();
    }

    [Fact]
    public void MarkCompleted_sets_the_answer_language_and_citations()
    {
        var message = new ChatMessage { Role = MessageRole.Assistant };
        var citation = new MessageCitation
        {
            Label = "E1",
            DocumentId = "doc-1",
            DocumentType = "SessionTranscript",
            Quote = "BP 120/80",
        };

        message.MarkCompleted("BP was normal [E1]", "en", [citation]);

        message.State.ShouldBe(MessageState.Completed);
        message.Content.ShouldBe("BP was normal [E1]");
        message.Language.ShouldBe("en");
        message.FailureReason.ShouldBeNull();
        message.Citations.ShouldHaveSingleItem().Label.ShouldBe("E1");
    }

    [Fact]
    public void MarkRefused_is_a_normal_answer_with_no_citations()
    {
        var message = new ChatMessage { Role = MessageRole.Assistant };

        message.MarkRefused("Insufficient evidence.", "en");

        message.State.ShouldBe(MessageState.Refused);
        message.FailureReason.ShouldBeNull();
        message.Citations.ShouldBeEmpty();
    }

    [Fact]
    public void MarkFailed_records_a_reason_and_retry_returns_it_to_pending()
    {
        var message = new ChatMessage { Role = MessageRole.Assistant };

        message.MarkFailed("The answer could not be generated.");
        message.State.ShouldBe(MessageState.Failed);
        message.FailureReason.ShouldBe("The answer could not be generated.");

        message.ResetForRetry();
        message.State.ShouldBe(MessageState.Pending);
        message.FailureReason.ShouldBeNull();
    }
}

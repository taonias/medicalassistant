using MedicalAssistant.Application.Features.Chat.Common;
using Shouldly;

namespace MedicalAssistant.Application.UnitTests.Chat;

public class ConversationTitleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_question_falls_back_to_a_default_title(string? question)
    {
        ConversationTitle.FromQuestion(question).ShouldBe("New conversation");
    }

    [Fact]
    public void Short_question_becomes_the_title_verbatim_with_collapsed_whitespace()
    {
        ConversationTitle.FromQuestion("  Any   penicillin allergy? ")
            .ShouldBe("Any penicillin allergy?");
    }

    [Fact]
    public void Only_the_first_line_is_used()
    {
        ConversationTitle.FromQuestion("What medications is he on?\nAlso his last BP?")
            .ShouldBe("What medications is he on? Also his last BP?");
    }

    [Fact]
    public void Long_question_is_truncated_at_a_word_boundary_and_ellipsized()
    {
        var question =
            "Summarize every cardiovascular finding across all of this patient's consultations since 2019";

        var title = ConversationTitle.FromQuestion(question);

        title.Length.ShouldBeLessThanOrEqualTo(61); // 60 + the ellipsis
        title.ShouldEndWith("…");
        title.ShouldStartWith("Summarize every cardiovascular");
        title.ShouldNotContain("  ");
    }
}

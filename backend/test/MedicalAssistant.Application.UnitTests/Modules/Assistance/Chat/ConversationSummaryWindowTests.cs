using MedicalAssistant.Application.Modules.Assistance.Chat;
using Shouldly;

namespace MedicalAssistant.Application.UnitTests.Chat;

public class ConversationSummaryWindowTests
{
    private const int Window = 6;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    public void No_refresh_while_the_thread_still_fits_the_verbatim_window(int maxSequence)
    {
        ConversationSummaryWindow.PlanFoldThrough(maxSequence, summarizedThrough: 0, Window)
            .ShouldBeNull();
    }

    [Fact]
    public void No_refresh_when_fewer_than_a_window_of_new_messages_since_last_summary()
    {
        // max 15, already summarized through 10 → foldThrough would be 9, which is < 10.
        // Only 5 (< window) genuinely-new messages have aged out since the last summary.
        ConversationSummaryWindow.PlanFoldThrough(maxSequence: 15, summarizedThrough: 10, Window)
            .ShouldBeNull();
    }

    [Fact]
    public void First_refresh_fires_once_the_thread_first_exceeds_the_window()
    {
        // 12 messages, never summarized → fold through 12 − 6 = 6, leaving the last 6 verbatim.
        ConversationSummaryWindow.PlanFoldThrough(maxSequence: 12, summarizedThrough: 0, Window)
            .ShouldBe(6);
    }

    [Fact]
    public void Refresh_boundary_marches_in_lockstep_with_the_window_no_gap_no_overlap()
    {
        // Summarized through 6; the thread has grown to 12 (a full window of new messages).
        // Fold through 6 again... which is not > summarizedThrough, so it is NOT yet due.
        ConversationSummaryWindow.PlanFoldThrough(maxSequence: 12, summarizedThrough: 6, Window)
            .ShouldBeNull();

        // One more message (13) tips it over: fold through 7, exactly one past the boundary.
        ConversationSummaryWindow.PlanFoldThrough(maxSequence: 13, summarizedThrough: 6, Window)
            .ShouldBeNull();

        // At 18 with last summary at 6, a full window has aged out → fold through 12.
        ConversationSummaryWindow.PlanFoldThrough(maxSequence: 18, summarizedThrough: 6, Window)
            .ShouldBe(12);
    }
}

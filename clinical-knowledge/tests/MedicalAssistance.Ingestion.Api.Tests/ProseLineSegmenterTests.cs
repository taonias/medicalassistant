using MedicalAssistance.Ingestion.Api.Ingestions;

namespace MedicalAssistance.Ingestion.Api.Tests;

/// <summary>
/// Regression coverage for the transcript-ingestion failure: a speech-to-text transcript
/// arrives as one unbroken paragraph, which the boundaries-only chunker cannot size. The
/// segmenter must break such a body into small, verbatim lines while leaving well-formed
/// input untouched. These are pure-function tests — no database or web host, so no Docker.
/// </summary>
public class ProseLineSegmenterTests
{
    // A real gpt-4o-transcribe transcript: one line, no newlines, well over the line budget.
    private const string SingleLineTranscript =
        "Good morning, what brings you in today? I have been getting these headaches almost every " +
        "morning for the past two weeks. They start behind my eyes and last a couple of hours. " +
        "How much water would you say you drink on a normal day? Honestly not much, maybe one or " +
        "two glasses, I drink a lot of coffee instead. Are the headaches worse on the days you " +
        "drink less water? Now that you mention it, yes, they seem worse after a busy shift. Let us " +
        "start with hydration and cutting back the afternoon coffee, and keep a short diary of when " +
        "they happen so we can look for a pattern at the next visit.";

    [Fact]
    public void An_unbroken_paragraph_becomes_several_lines_each_within_the_budget()
    {
        var lines = ProseLineSegmenter.Segment(SingleLineTranscript);

        // The whole point: one giant line must become many the chunker can group.
        Assert.True(lines.Count > 1, $"expected multiple lines, got {lines.Count}");
        Assert.All(lines, line => Assert.True(
            line.Length <= ProseLineSegmenter.MaxLineCharacters,
            $"line exceeds the budget ({line.Length} chars): {line}"));
    }

    [Fact]
    public void Segmenting_preserves_every_word_in_order_verbatim()
    {
        var lines = ProseLineSegmenter.Segment(SingleLineTranscript);

        // Words (and their order) must survive: the segmenter only turns whitespace into line
        // breaks, it never rewrites patient text.
        var originalWords = Words(SingleLineTranscript);
        var segmentedWords = Words(string.Join(" ", lines));
        Assert.Equal(originalWords, segmentedWords);
    }

    [Fact]
    public void Well_formed_multi_line_input_is_left_untouched()
    {
        var note = "Patient reports morning headaches.\nAdvised hydration and less coffee.\nFollow up in two weeks.";

        var lines = ProseLineSegmenter.Segment(note);

        Assert.Equal(
            ["Patient reports morning headaches.", "Advised hydration and less coffee.", "Follow up in two weeks."],
            lines);
    }

    [Fact]
    public void A_long_run_without_sentence_punctuation_is_still_wrapped_by_words()
    {
        // Some ASR output has no punctuation at all — sentence splitting finds no boundary, so
        // the word-wrap fallback must still keep every line within the budget.
        var unpunctuated = string.Join(" ", Enumerable.Repeat("word", 200));

        var lines = ProseLineSegmenter.Segment(unpunctuated);

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.True(line.Length <= ProseLineSegmenter.MaxLineCharacters));
        Assert.Equal(Words(unpunctuated), Words(string.Join(" ", lines)));
    }

    [Fact]
    public void Decimal_points_do_not_split_a_sentence()
    {
        // "3.5" must not be read as a sentence boundary. Build one oversized line so the
        // segmenter engages, then confirm the decimal token stays intact on some line.
        var body = "The dose was increased to 3.5 milligrams taken twice daily. " +
                   string.Join(" ", Enumerable.Repeat("Follow up as needed.", 20));

        var lines = ProseLineSegmenter.Segment(body);

        Assert.Contains(lines, line => line.Contains("3.5 milligrams"));
    }

    private static string[] Words(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}

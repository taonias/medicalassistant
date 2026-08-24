using System.Text;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// Normalizes a free-text body into the numbered lines the boundaries-only chunker consumes.
/// Splits on existing newlines and drops blanks, then breaks any oversized line into
/// sentence-sized, verbatim lines. A speech-to-text transcript arrives as one unbroken
/// paragraph with no newlines; without this the chunker gets a single giant line it cannot
/// size (the size guardrails only ever move boundaries to existing line breaks), which is what
/// made a real transcript fail to ingest. Lines already within budget are left untouched, so
/// well-formed input (a note with real line breaks) is unchanged.
/// </summary>
public static class ProseLineSegmenter
{
    /// <summary>
    /// The per-line character budget. Kept well under the chunk token ceiling so the chunker
    /// and its size guardrails always have several line boundaries to group into a chunk.
    /// </summary>
    public const int MaxLineCharacters = 320;

    public static IReadOnlyList<string> Segment(string body) =>
        body
            .Split('\n')
            .Select(line => line.TrimEnd('\r').Trim())
            .Where(line => line.Length > 0)
            .SelectMany(SegmentLongLine)
            .ToList();

    private static IEnumerable<string> SegmentLongLine(string line)
    {
        if (line.Length <= MaxLineCharacters)
        {
            yield return line;
            yield break;
        }

        foreach (var sentence in SplitIntoSentences(line))
        {
            if (sentence.Length <= MaxLineCharacters)
            {
                yield return sentence;
                continue;
            }

            // A sentence with no internal punctuation can still exceed the budget (unpunctuated
            // ASR output); wrap it at word boundaries so no line stays oversized.
            foreach (var wrapped in WrapByWords(sentence, MaxLineCharacters))
                yield return wrapped;
        }
    }

    private static IEnumerable<string> SplitIntoSentences(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('.' or '!' or '?'))
                continue;

            // Consume a run of terminators (e.g. "?!"), then treat it as a sentence end only
            // when followed by whitespace or the end of the text. This keeps decimals ("3.5")
            // and other mid-token dots intact.
            var last = i;
            while (last + 1 < text.Length && text[last + 1] is '.' or '!' or '?')
                last++;

            if (last + 1 >= text.Length || char.IsWhiteSpace(text[last + 1]))
            {
                var sentence = text[start..(last + 1)].Trim();
                if (sentence.Length > 0)
                    yield return sentence;
                start = last + 1;
                i = last;
            }
        }

        if (start < text.Length)
        {
            var tail = text[start..].Trim();
            if (tail.Length > 0)
                yield return tail;
        }
    }

    private static IEnumerable<string> WrapByWords(string text, int maxChars)
    {
        var builder = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (builder.Length > 0 && builder.Length + 1 + word.Length > maxChars)
            {
                yield return builder.ToString();
                builder.Clear();
            }

            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(word);
        }

        if (builder.Length > 0)
            yield return builder.ToString();
    }
}

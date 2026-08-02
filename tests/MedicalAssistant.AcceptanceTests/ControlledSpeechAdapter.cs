using System.Collections.Concurrent;

namespace MedicalAssistant.AcceptanceTests;

/// <summary>
/// Deterministic Azure Speech seam for end-to-end tests. The worker adapter
/// introduced later can delegate to this script without tests learning its internals.
/// </summary>
public sealed class ControlledSpeechAdapter
{
    private readonly ConcurrentQueue<Func<CancellationToken, Task<string>>> _script = new();

    public void EnqueueTranscript(string transcript)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);
        _script.Enqueue(_ => Task.FromResult(transcript));
    }

    public Action EnqueueBlockedTranscript(string transcript)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _script.Enqueue(async cancellationToken =>
        {
            await gate.Task.WaitAsync(cancellationToken);
            return transcript;
        });
        return () => gate.TrySetResult();
    }

    public async Task<string> TranscribeAsync(
        SyntheticRecording recording,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);
        if (recording.Content.Length == 0)
            throw new ArgumentException("Synthetic audio must not be empty.", nameof(recording));
        if (!_script.TryDequeue(out var response))
            throw new InvalidOperationException("No controlled Speech response has been scripted.");
        return await response(cancellationToken);
    }
}

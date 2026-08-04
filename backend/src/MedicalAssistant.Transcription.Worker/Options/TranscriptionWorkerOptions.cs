using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Options;

public sealed class TranscriptionWorkerOptions
{
    public const string SectionName = "TranscriptionWorker";
    public const int MaximumConcurrentTranscriptions = 32;

    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ProcessingLeaseDuration { get; set; } = TimeSpan.FromMinutes(10);
    public int MaxConcurrentTranscriptions { get; set; } = 1;
}

public sealed class TranscriptionWorkerOptionsValidator : IValidateOptions<TranscriptionWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, TranscriptionWorkerOptions options)
    {
        var failures = new List<string>();

        if (options.MaxConcurrentTranscriptions < 1)
        {
            failures.Add($"{TranscriptionWorkerOptions.SectionName}:{nameof(options.MaxConcurrentTranscriptions)} must be at least 1.");
        }

        if (options.MaxConcurrentTranscriptions > TranscriptionWorkerOptions.MaximumConcurrentTranscriptions)
        {
            failures.Add($"{TranscriptionWorkerOptions.SectionName}:{nameof(options.MaxConcurrentTranscriptions)} must be {TranscriptionWorkerOptions.MaximumConcurrentTranscriptions} or less.");
        }

        if (options.ShutdownDrainTimeout <= TimeSpan.Zero)
        {
            failures.Add($"{TranscriptionWorkerOptions.SectionName}:{nameof(options.ShutdownDrainTimeout)} must be positive.");
        }

        if (options.ProcessingLeaseDuration <= TimeSpan.Zero)
        {
            failures.Add($"{TranscriptionWorkerOptions.SectionName}:{nameof(options.ProcessingLeaseDuration)} must be positive.");
        }

        if (options.ProcessingLeaseDuration <= options.ShutdownDrainTimeout)
        {
            failures.Add($"{TranscriptionWorkerOptions.SectionName}:{nameof(options.ProcessingLeaseDuration)} must be longer than {nameof(options.ShutdownDrainTimeout)} so shutdown drain cannot outlive the database processing lease.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

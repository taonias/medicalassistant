using MedicalAssistant.Transcription.Worker.Options;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class TranscriptionWorkerOptionsValidatorTests
{
    [Fact]
    public void Validator_accepts_safe_worker_concurrency_defaults()
    {
        var validator = new TranscriptionWorkerOptionsValidator();

        var result = validator.Validate(null, new TranscriptionWorkerOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_rejects_worker_concurrency_that_can_exceed_quota_or_lease_safety()
    {
        var validator = new TranscriptionWorkerOptionsValidator();

        var result = validator.Validate(null, new TranscriptionWorkerOptions
        {
            MaxConcurrentTranscriptions = TranscriptionWorkerOptions.MaximumConcurrentTranscriptions + 1,
            ShutdownDrainTimeout = TimeSpan.FromMinutes(10),
            ProcessingLeaseDuration = TimeSpan.FromMinutes(10)
        });

        Assert.False(result.Succeeded);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);
        Assert.Contains(failures, failure => failure.Contains(nameof(TranscriptionWorkerOptions.MaxConcurrentTranscriptions), StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains(nameof(TranscriptionWorkerOptions.ProcessingLeaseDuration), StringComparison.Ordinal));
    }
}

using MedicalAssistant.Application.Models;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.UnitTests.Features;

public class ConsultationOutboxRelayOptionsValidatorTests
{
    [Fact]
    public void Validator_accepts_safe_scaling_defaults()
    {
        var validator = new ConsultationOutboxRelayOptionsValidator();

        var result = validator.Validate(null, new ConsultationOutboxRelayOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_rejects_unbounded_or_non_positive_outbox_scaling_settings()
    {
        var validator = new ConsultationOutboxRelayOptionsValidator();

        var result = validator.Validate(null, new ConsultationOutboxRelayOptions
        {
            BatchSize = ConsultationOutboxRelayOptions.MaximumBatchSize + 1,
            LeaseDuration = TimeSpan.Zero,
            PollInterval = TimeSpan.Zero,
            FailureBackoff = TimeSpan.Zero,
            LeaseOwner = " "
        });

        Assert.False(result.Succeeded);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);
        Assert.Contains(failures, failure => failure.Contains(nameof(ConsultationOutboxRelayOptions.BatchSize), StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains(nameof(ConsultationOutboxRelayOptions.LeaseDuration), StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains(nameof(ConsultationOutboxRelayOptions.PollInterval), StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains(nameof(ConsultationOutboxRelayOptions.FailureBackoff), StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains(nameof(ConsultationOutboxRelayOptions.LeaseOwner), StringComparison.Ordinal));
    }
}

using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Models;

public sealed class ConsultationOutboxRelayOptions
{
    public const string SectionName = "ConsultationOutboxRelay";
    public const int MaximumBatchSize = 1_000;

    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 25;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan FailureBackoff { get; set; } = TimeSpan.FromSeconds(30);
    public string LeaseOwner { get; set; } = $"medicalassistant-outbox-{Environment.MachineName}";
}

public sealed class ConsultationOutboxRelayOptionsValidator : IValidateOptions<ConsultationOutboxRelayOptions>
{
    public ValidateOptionsResult Validate(string? name, ConsultationOutboxRelayOptions options)
    {
        var failures = new List<string>();

        if (options.BatchSize <= 0)
        {
            failures.Add($"{ConsultationOutboxRelayOptions.SectionName}:{nameof(options.BatchSize)} must be positive.");
        }

        if (options.BatchSize > ConsultationOutboxRelayOptions.MaximumBatchSize)
        {
            failures.Add($"{ConsultationOutboxRelayOptions.SectionName}:{nameof(options.BatchSize)} must be {ConsultationOutboxRelayOptions.MaximumBatchSize} or less.");
        }

        if (options.LeaseDuration <= TimeSpan.Zero)
        {
            failures.Add($"{ConsultationOutboxRelayOptions.SectionName}:{nameof(options.LeaseDuration)} must be positive.");
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add($"{ConsultationOutboxRelayOptions.SectionName}:{nameof(options.PollInterval)} must be positive.");
        }

        if (options.FailureBackoff <= TimeSpan.Zero)
        {
            failures.Add($"{ConsultationOutboxRelayOptions.SectionName}:{nameof(options.FailureBackoff)} must be positive.");
        }

        if (string.IsNullOrWhiteSpace(options.LeaseOwner))
        {
            failures.Add($"{ConsultationOutboxRelayOptions.SectionName}:{nameof(options.LeaseOwner)} must be provided.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

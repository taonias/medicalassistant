using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Models;

public sealed class EventRetentionOptions
{
    public const string SectionName = "EventRetention";

    public TimeSpan OutboxRetention { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan InboxRetention { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan DeadLetterRetention { get; set; } = TimeSpan.FromDays(14);
    public TimeSpan TombstoneRetention { get; set; } = TimeSpan.FromDays(90);
}

public sealed class EventRetentionOptionsValidator : IValidateOptions<EventRetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, EventRetentionOptions options)
    {
        var failures = new List<string>();

        if (options.OutboxRetention <= TimeSpan.Zero)
            failures.Add("EventRetention:OutboxRetention must be positive.");

        if (options.InboxRetention <= TimeSpan.Zero)
            failures.Add("EventRetention:InboxRetention must be positive.");

        if (options.DeadLetterRetention <= TimeSpan.Zero)
            failures.Add("EventRetention:DeadLetterRetention must be positive.");

        if (options.TombstoneRetention < options.OutboxRetention ||
            options.TombstoneRetention < options.InboxRetention ||
            options.TombstoneRetention < options.DeadLetterRetention)
        {
            failures.Add("EventRetention:TombstoneRetention must be at least as long as outbox, inbox, and dead-letter retention.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

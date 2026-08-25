using Microsoft.Extensions.Options;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqConsumerOptions
{
    public const string SectionName = "RabbitMQ:Consumer";

    public string QueueName { get; set; } = string.Empty;
    public ushort PrefetchCount { get; set; } = 1;
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class RabbitMqConsumerOptionsValidator : IValidateOptions<RabbitMqConsumerOptions>
{
    public ValidateOptionsResult Validate(string? name, RabbitMqConsumerOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.QueueName))
        {
            failures.Add($"{RabbitMqConsumerOptions.SectionName}:{nameof(options.QueueName)} must be provided.");
        }

        if (options.PrefetchCount == 0)
        {
            failures.Add($"{RabbitMqConsumerOptions.SectionName}:{nameof(options.PrefetchCount)} must be positive.");
        }

        if (options.ShutdownDrainTimeout <= TimeSpan.Zero)
        {
            failures.Add($"{RabbitMqConsumerOptions.SectionName}:{nameof(options.ShutdownDrainTimeout)} must be positive.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

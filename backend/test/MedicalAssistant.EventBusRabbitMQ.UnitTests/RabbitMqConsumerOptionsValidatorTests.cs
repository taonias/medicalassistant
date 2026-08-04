using MedicalAssistant.EventBusRabbitMQ;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class RabbitMqConsumerOptionsValidatorTests
{
    [Fact]
    public void Validator_accepts_safe_consumer_settings()
    {
        var validator = new RabbitMqConsumerOptionsValidator();

        var result = validator.Validate(null, new RabbitMqConsumerOptions
        {
            QueueName = "medicalassistant.transcription-worker",
            PrefetchCount = 1,
            ShutdownDrainTimeout = TimeSpan.FromSeconds(30)
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_rejects_consumers_without_queue_prefetch_or_shutdown_drain()
    {
        var validator = new RabbitMqConsumerOptionsValidator();

        var result = validator.Validate(null, new RabbitMqConsumerOptions
        {
            QueueName = " ",
            PrefetchCount = 0,
            ShutdownDrainTimeout = TimeSpan.Zero
        });

        Assert.False(result.Succeeded);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);
        Assert.Contains(failures, failure => failure.Contains(nameof(RabbitMqConsumerOptions.QueueName), StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains(nameof(RabbitMqConsumerOptions.PrefetchCount), StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains(nameof(RabbitMqConsumerOptions.ShutdownDrainTimeout), StringComparison.Ordinal));
    }
}

using System.Text.Json;
using MedicalAssistant.AiModule.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.AiModule.Functions;

public sealed class ProcessAiRequestFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILogger<ProcessAiRequestFunction> _logger;

    public ProcessAiRequestFunction(ILogger<ProcessAiRequestFunction> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Triggered by RabbitMQ <c>ai.requests</c>. On success the Functions host acknowledges
    /// the message and removes it from the queue.
    /// </summary>
    [Function(nameof(ProcessAiRequest))]
    public Task ProcessAiRequest(
        [RabbitMQTrigger("%RabbitMqQueueName%", ConnectionStringSetting = "RabbitMqConnection")]
        string queueMessage,
        FunctionContext context)
    {
        _logger.LogInformation("Received RabbitMQ message ({Length} bytes).", queueMessage.Length);

        TranscriptReadyMessage message;
        try
        {
            message = JsonSerializer.Deserialize<TranscriptReadyMessage>(queueMessage, JsonOptions)
                ?? throw new InvalidOperationException("AI request message deserialized to null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid transcript-ready message JSON.");
            throw;
        }

        _logger.LogInformation(
            "Deserialized {EventType} for consultation {ConsultationId}, transcript {TranscriptId}, correlation {CorrelationId} (invocation {InvocationId}).",
            message.EventType,
            message.ConsultationId,
            message.TranscriptId,
            message.CorrelationId,
            context.InvocationId);

        return Task.CompletedTask;
    }
}

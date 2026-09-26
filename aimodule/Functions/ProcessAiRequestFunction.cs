using System.Text.Json;
using MedicalAssistant.AiModule.Models;
using MedicalAssistant.AiModule.Services;
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

    private readonly IAiResultPublisher _resultPublisher;
    private readonly ILogger<ProcessAiRequestFunction> _logger;

    public ProcessAiRequestFunction(
        IAiResultPublisher resultPublisher,
        ILogger<ProcessAiRequestFunction> logger)
    {
        _resultPublisher = resultPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Triggered by RabbitMQ <c>ai.requests</c>. Deserializes chat or transcript-ready
    /// messages and publishes a dummy result to <c>ai.results</c> for backend testing.
    /// </summary>
    [Function(nameof(ProcessAiRequest))]
    public async Task ProcessAiRequest(
        [RabbitMQTrigger("%RabbitMqQueueName%", ConnectionStringSetting = "RabbitMqConnection")]
        string queueMessage,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received RabbitMQ message ({Length} bytes).", queueMessage.Length);

        AiRequestEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<AiRequestEnvelope>(queueMessage, JsonOptions)
                ?? throw new InvalidOperationException("AI request message deserialized to null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid AI request message JSON.");
            throw;
        }

        switch (envelope.EventType)
        {
            case AiRequestEventTypes.ChatRequested:
                await HandleChatAsync(queueMessage, context.InvocationId, cancellationToken);
                break;
            case TranscriptReadyEventTypes.Ready:
                await HandleTranscriptReadyAsync(queueMessage, context.InvocationId, cancellationToken);
                break;
            default:
                _logger.LogWarning(
                    "Ignoring unsupported AI request {EventType} (correlation {CorrelationId}, invocation {InvocationId}).",
                    envelope.EventType,
                    envelope.CorrelationId,
                    context.InvocationId);
                break;
        }
    }

    private async Task HandleChatAsync(string queueMessage, string invocationId, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<ChatRequestedMessage>(queueMessage, JsonOptions)
            ?? throw new InvalidOperationException("Chat request message deserialized to null.");

        _logger.LogInformation(
            "Deserialized {EventType} chat {ChatRequestId} (correlation {CorrelationId}, invocation {InvocationId}).",
            message.EventType,
            message.ChatRequestId,
            message.CorrelationId,
            invocationId);

        await _resultPublisher.PublishAsync(new AiResultMessage
        {
            EventType = AiResultEventTypes.ChatCompleted,
            CorrelationId = message.CorrelationId,
            Answer = $"Dummy chat response for debugging. Echo: {message.Message}",
            Citations = ["Dummy citation"],
            SuggestedActions = ["SummarizeConsultation"],
        }, cancellationToken);
    }

    private async Task HandleTranscriptReadyAsync(
        string queueMessage,
        string invocationId,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TranscriptReadyMessage>(queueMessage, JsonOptions)
            ?? throw new InvalidOperationException("Transcript-ready message deserialized to null.");

        _logger.LogInformation(
            "Deserialized {EventType} for consultation {ConsultationId}, transcript {TranscriptId}, correlation {CorrelationId} (invocation {InvocationId}).",
            message.EventType,
            message.ConsultationId,
            message.TranscriptId,
            message.CorrelationId,
            invocationId);

        await _resultPublisher.PublishAsync(new AiResultMessage
        {
            EventType = AiResultEventTypes.StructuredDataCompleted,
            CorrelationId = message.CorrelationId,
            ConsultationId = message.ConsultationId,
            TranscriptId = message.TranscriptId,
            SchemaVersion = "v1",
            StructuredPayload = """{"summary":"Dummy structured data for debugging.","diagnoses":["Placeholder diagnosis"],"medications":["Placeholder medication"]}""",
        }, cancellationToken);
    }
}

using System.Diagnostics;
using System.Text.Json;
using MedicalAssistant.Transcriber.Models;
using MedicalAssistant.Transcriber.Services;
using MedicalAssistant.Transcriber.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Transcriber.Functions;

public sealed class ProcessConsultationFileFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConsultationFileRetriever _fileRetriever;
    private readonly IAuditTrailService _auditTrail;
    private readonly ITranscriptService _transcriptService;
    private readonly ILogger<ProcessConsultationFileFunction> _logger;

    public ProcessConsultationFileFunction(
        IConsultationFileRetriever fileRetriever,
        IAuditTrailService auditTrail,
        ITranscriptService transcriptService,
        ILogger<ProcessConsultationFileFunction> logger)
    {
        _fileRetriever = fileRetriever;
        _auditTrail = auditTrail;
        _transcriptService = transcriptService;
        _logger = logger;
    }

    /// <summary>
    /// Triggered by RabbitMQ. On successful completion the Functions host acknowledges
    /// the message and removes it from the queue.
    /// </summary>
    [Function(nameof(ProcessConsultationFile))]
    public async Task ProcessConsultationFile(
        [RabbitMQTrigger("%RabbitMqQueueName%", ConnectionStringSetting = "RabbitMqConnection")]
        string queueMessage,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ConsultationProcessingMessage? message = null;

        try
        {
            await _auditTrail.LogAsync(
                TranscriberAuditActions.ProcessStarted,
                details: new
                {
                    QueueMessageLength = queueMessage.Length,
                    StartedAtUtc = DateTime.UtcNow,
                },
                entityType: "Transcriber",
                entityId: context.InvocationId,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Received RabbitMQ message ({Length} bytes).", queueMessage.Length);

            try
            {
                message = JsonSerializer.Deserialize<ConsultationProcessingMessage>(queueMessage, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid consultation processing message JSON.");
                await _auditTrail.LogAsync(
                    TranscriberAuditActions.ProcessFailed,
                    details: new
                    {
                        Stage = "Deserialize",
                        Error = ex.Message,
                        DurationMs = stopwatch.ElapsedMilliseconds,
                    },
                    entityType: "Transcriber",
                    entityId: context.InvocationId,
                    cancellationToken: CancellationToken.None);
                throw;
            }

            if (message is null)
                throw new InvalidOperationException("Consultation processing message deserialized to null.");

            await _auditTrail.LogAsync(
                TranscriberAuditActions.MessageParsed,
                message,
                details: new
                {
                    message.EventType,
                    message.CorrelationId,
                    message.FileType,
                    message.Status,
                    message.BlobUri,
                    message.ContentType,
                    message.FileName,
                    message.DurationSeconds,
                    message.OccurredAtUtc,
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Processing consultation {ConsultationId} ({FileType}) correlation {CorrelationId}.",
                message.ConsultationId,
                message.FileType,
                message.CorrelationId);

            await _auditTrail.LogAsync(
                TranscriberAuditActions.BlobRetrieveStarted,
                message,
                details: new { message.BlobUri },
                cancellationToken: cancellationToken);

            var file = await _fileRetriever.RetrieveAsync(message, cancellationToken);

            await _auditTrail.LogAsync(
                TranscriberAuditActions.BlobRetrieved,
                message,
                details: new
                {
                    file.FileType,
                    file.BlobUri,
                    file.Container,
                    file.BlobName,
                    file.FileName,
                    file.ContentType,
                    file.ByteLength,
                    RetrievedAtUtc = DateTime.UtcNow,
                },
                cancellationToken: cancellationToken);

            await _transcriptService.CreateFromRetrievedFileAsync(message, file, cancellationToken);

            stopwatch.Stop();
            await _auditTrail.LogAsync(
                TranscriberAuditActions.ProcessCompleted,
                message,
                details: new
                {
                    file.FileType,
                    file.BlobName,
                    file.ByteLength,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    CompletedAtUtc = DateTime.UtcNow,
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Retrieved {FileType} blob {BlobName} ({ByteLength} bytes) for consultation {ConsultationId}; audit and transcript written.",
                file.FileType,
                file.BlobName,
                file.ByteLength,
                message.ConsultationId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Transcriber process failed.");

            try
            {
                await _auditTrail.LogAsync(
                    TranscriberAuditActions.ProcessFailed,
                    message,
                    details: new
                    {
                        Error = ex.Message,
                        ExceptionType = ex.GetType().FullName,
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        FailedAtUtc = DateTime.UtcNow,
                    },
                    entityType: message is null ? "Transcriber" : "Consultation",
                    entityId: message?.ConsultationId.ToString() ?? context.InvocationId,
                    cancellationToken: CancellationToken.None);
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "Failed to write ProcessFailed audit log.");
            }

            throw;
        }
    }
}

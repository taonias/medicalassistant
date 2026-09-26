using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Features.Chat.Command.ProcessChatResult;
using MedicalAssistant.Application.Features.MedicalStructuredData.Command.ProcessStructuredDataCallback;
using MedicalAssistant.Application.Models.Messaging;
using MediatR;

namespace MedicalAssistant.Application.Messaging;

public sealed class AiResultProcessor : IAiResultProcessor
{
    private readonly IMediator _mediator;

    public AiResultProcessor(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task ProcessAsync(AiResultMessage message, CancellationToken cancellationToken = default)
    {
        return message.EventType switch
        {
            AiResultEventTypes.ChatCompleted => _mediator.Send(new ProcessChatResultCommand
            {
                CorrelationId = message.CorrelationId,
                Status = "completed",
                Answer = message.Answer,
                Citations = message.Citations,
                SuggestedActions = message.SuggestedActions
            }, cancellationToken),
            AiResultEventTypes.ChatFailed => _mediator.Send(new ProcessChatResultCommand
            {
                CorrelationId = message.CorrelationId,
                Status = "failed",
                FailureReason = message.FailureReason
            }, cancellationToken),
            AiResultEventTypes.ActionCompleted or AiResultEventTypes.ActionFailed
                => Task.CompletedTask,
            AiResultEventTypes.StructuredDataCompleted => _mediator.Send(new ProcessStructuredDataCallbackCommand
            {
                JobId = message.CorrelationId,
                CorrelationId = message.CorrelationId,
                ConsultationId = message.ConsultationId,
                TranscriptId = message.TranscriptId,
                SchemaVersion = message.SchemaVersion ?? "v1",
                StructuredPayload = message.StructuredPayload ?? "{}",
                Status = "completed"
            }, cancellationToken),
            AiResultEventTypes.StructuredDataFailed => _mediator.Send(new ProcessStructuredDataCallbackCommand
            {
                JobId = message.CorrelationId,
                CorrelationId = message.CorrelationId,
                ConsultationId = message.ConsultationId,
                TranscriptId = message.TranscriptId,
                SchemaVersion = message.SchemaVersion ?? "v1",
                StructuredPayload = message.StructuredPayload ?? "{}",
                Status = "failed",
                FailureReason = message.FailureReason
            }, cancellationToken),
            AiResultEventTypes.DocumentsIndexCompleted or AiResultEventTypes.DocumentsIndexFailed
                => Task.CompletedTask,
            _ => throw new InvalidOperationException($"Unsupported AI result event '{message.EventType}'.")
        };
    }
}

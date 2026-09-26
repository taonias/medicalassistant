using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace MedicalAssistant.Application.Features.Chat.Command.ProcessChatResult;

public class ProcessChatResultCommandHandler : IRequestHandler<ProcessChatResultCommand, Unit>
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly IChatRequestRepository _chatRequestRepository;

    public ProcessChatResultCommandHandler(IChatRequestRepository chatRequestRepository)
    {
        _chatRequestRepository = chatRequestRepository;
    }

    public async Task<Unit> Handle(ProcessChatResultCommand request, CancellationToken cancellationToken)
    {
        var chat = await _chatRequestRepository.GetByCorrelationIdAsync(request.CorrelationId);
        if (chat == null)
            return Unit.Value;

        if (chat.Status is ChatRequestStatus.Completed or ChatRequestStatus.Failed)
            return Unit.Value;

        if (string.Equals(request.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            chat.MarkCompleted(
                request.Answer ?? string.Empty,
                JsonSerializer.Serialize(request.Citations, JsonOptions),
                JsonSerializer.Serialize(request.SuggestedActions, JsonOptions));
        }
        else if (string.Equals(request.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            chat.MarkFailed(request.FailureReason ?? "Chat failed");
        }

        await _chatRequestRepository.UpdateAsync(chat);
        return Unit.Value;
    }
}

using MedicalAssistant.Application.Features.Chat.Command.ProcessChatResult;
using MedicalAssistant.Application.Messaging;
using MedicalAssistant.Application.Models.Messaging;
using MediatR;
using Moq;

namespace MedicalAssistant.Application.UnitTests.Features;

public class AiResultProcessorTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task Process_dispatches_chat_completion_command()
    {
        var processor = new AiResultProcessor(_mediator.Object);
        await processor.ProcessAsync(new AiResultMessage
        {
            EventType = AiResultEventTypes.ChatCompleted,
            CorrelationId = "chat-1",
            Answer = "Hello doctor",
            Citations = ["Consultation #1"],
            SuggestedActions = ["SummarizeConsultation"]
        });

        _mediator.Verify(m => m.Send(It.Is<ProcessChatResultCommand>(c =>
            c.CorrelationId == "chat-1" &&
            c.Status == "completed" &&
            c.Answer == "Hello doctor"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_ignores_action_completion()
    {
        var processor = new AiResultProcessor(_mediator.Object);
        await processor.ProcessAsync(new AiResultMessage
        {
            EventType = AiResultEventTypes.ActionCompleted,
            CorrelationId = "act-1",
            ResponsePayload = "{\"ok\":true}"
        });

        _mediator.Verify(m => m.Send(It.IsAny<IRequest<Unit>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Features.Chat.Command.ProcessChatResult;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Moq;
using Shouldly;

namespace MedicalAssistant.Application.UnitTests.Features;

public class ProcessChatResultCommandHandlerTests
{
    private readonly Mock<IChatRequestRepository> _chatRequests = new();

    [Fact]
    public async Task Handle_completes_pending_chat()
    {
        var chat = new ChatRequest
        {
            CorrelationId = "chat-1",
            DoctorId = "doctor-1",
            Message = "Hi",
            Status = ChatRequestStatus.Pending
        };
        _chatRequests.Setup(r => r.GetByCorrelationIdAsync("chat-1")).ReturnsAsync(chat);

        var handler = new ProcessChatResultCommandHandler(_chatRequests.Object);
        await handler.Handle(new ProcessChatResultCommand
        {
            CorrelationId = "chat-1",
            Status = "completed",
            Answer = "Hello doctor",
            Citations = ["Consultation #1"],
            SuggestedActions = ["SummarizeConsultation"]
        }, CancellationToken.None);

        chat.Status.ShouldBe(ChatRequestStatus.Completed);
        chat.Answer.ShouldBe("Hello doctor");
        _chatRequests.Verify(r => r.UpdateAsync(chat), Times.Once);
    }

    [Fact]
    public async Task Handle_ignores_duplicate_completed_chat()
    {
        var chat = new ChatRequest
        {
            CorrelationId = "chat-1",
            DoctorId = "doctor-1",
            Message = "Hi",
            Status = ChatRequestStatus.Completed,
            Answer = "Original"
        };
        _chatRequests.Setup(r => r.GetByCorrelationIdAsync("chat-1")).ReturnsAsync(chat);

        var handler = new ProcessChatResultCommandHandler(_chatRequests.Object);
        await handler.Handle(new ProcessChatResultCommand
        {
            CorrelationId = "chat-1",
            Status = "completed",
            Answer = "Replacement"
        }, CancellationToken.None);

        chat.Answer.ShouldBe("Original");
        _chatRequests.Verify(r => r.UpdateAsync(It.IsAny<ChatRequest>()), Times.Never);
    }
}

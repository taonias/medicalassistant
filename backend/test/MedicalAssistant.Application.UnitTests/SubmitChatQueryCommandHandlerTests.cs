using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Features.Chat.Command.SubmitChatQuery;
using MedicalAssistant.Application.MappingProfiles;
using MedicalAssistant.Application.Models.Messaging;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Moq;
using Shouldly;

namespace MedicalAssistant.Application.UnitTests.Features;

public class SubmitChatQueryCommandHandlerTests
{
    private readonly Mock<IAiRequestPublisher> _publisher = new();
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IPatientRepository> _patients = new();
    private readonly Mock<IConsultationRepository> _consultations = new();
    private readonly Mock<IChatRequestRepository> _chatRequests = new();
    private readonly IMapper _mapper = new MapperConfiguration(cfg => cfg.AddProfile<ChatRequestProfile>()).CreateMapper();

    public SubmitChatQueryCommandHandlerTests()
    {
        _userService.Setup(s => s.GetCurrentUserIdAsync()).ReturnsAsync("doctor-1");
        _chatRequests
            .Setup(r => r.CreateAsync(It.IsAny<ChatRequest>()))
            .ReturnsAsync((ChatRequest entity) =>
            {
                entity.Id = 11;
                return entity;
            });
    }

    private SubmitChatQueryCommandHandler CreateHandler() => new(
        _publisher.Object,
        _userService.Object,
        _patients.Object,
        _consultations.Object,
        _chatRequests.Object,
        _mapper);

    [Fact]
    public async Task Handle_publishes_chat_requested_not_transcript_ready()
    {
        ChatRequestedMessage? published = null;
        _publisher
            .Setup(p => p.PublishChatAsync(It.IsAny<ChatRequestedMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequestedMessage, CancellationToken>((message, _) => published = message)
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(
            new SubmitChatQueryCommand { Message = "Summarize", SessionId = "session-1" },
            CancellationToken.None);

        result.Status.ShouldBe(ChatRequestStatus.Pending.ToString());
        result.CorrelationId.ShouldNotBeNullOrWhiteSpace();
        result.Answer.ShouldBeNull();

        published.ShouldNotBeNull();
        published!.EventType.ShouldBe(AiRequestEventTypes.ChatRequested);
        published.EventType.ShouldNotBe(TranscriptReadyEventTypes.Ready);
        published.Message.ShouldBe("Summarize");
        published.DoctorId.ShouldBe("doctor-1");
        published.ChatRequestId.ShouldBe(11);
        published.SessionId.ShouldBe("session-1");
    }

    [Fact]
    public async Task Handle_marks_failed_when_publish_throws()
    {
        _publisher
            .Setup(p => p.PublishChatAsync(It.IsAny<ChatRequestedMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var handler = CreateHandler();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.Handle(new SubmitChatQueryCommand { Message = "Hello" }, CancellationToken.None));

        _chatRequests.Verify(r => r.UpdateAsync(It.Is<ChatRequest>(c =>
            c.Status == ChatRequestStatus.Failed &&
            c.FailureReason != null)), Times.Once);
    }
}

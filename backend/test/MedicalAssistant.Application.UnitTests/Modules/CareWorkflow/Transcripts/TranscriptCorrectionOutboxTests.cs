using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Features.Transcript.Command.UpdateTranscript;
using MedicalAssistant.Application.MappingProfiles;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Moq;

namespace MedicalAssistant.Application.UnitTests.Features;

public class TranscriptCorrectionOutboxTests
{
    [Fact]
    public async Task Transcript_correction_increments_revision_and_stages_transcript_ready_outbox()
    {
        var consultation = new Consultation
        {
            Id = 7,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/7/audio"
        };
        var transcript = new Transcript
        {
            Id = 70,
            ConsultationId = 7,
            Status = TranscriptStatus.Completed,
            TranscriptText = "old text",
            Revision = 1
        };
        var transcriptRepository = new Mock<ITranscriptRepository>();
        transcriptRepository
            .Setup(r => r.GetByConsultationIdAsync(7))
            .ReturnsAsync(transcript);
        var consultationRepository = new Mock<IConsultationAccess>();
        consultationRepository
            .Setup(r => r.GetConsultationForDoctorAsync(7, "doctor-1"))
            .ReturnsAsync(consultation);
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetCurrentUserIdAsync()).ReturnsAsync("doctor-1");
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<TranscriptProfile>()).CreateMapper();
        var handler = new UpdateTranscriptCommandHandler(
            transcriptRepository.Object,
            consultationRepository.Object,
            userService.Object,
            mapper);

        await handler.Handle(
            new UpdateTranscriptCommand { ConsultationId = 7, Transcript = "new text" },
            CancellationToken.None);

        Assert.Equal(2, transcript.Revision);
        transcriptRepository.Verify(r => r.UpdateWithOutboxAsync(
            transcript,
            consultation,
            It.Is<ConsultationOutboxMessage>(message =>
                message.EventType == "consultation.transcript-ready.v1" &&
            message.Payload.Contains("\"transcriptId\":70") &&
            message.Payload.Contains("\"transcriptRevision\":2") &&
            !message.Payload.Contains("new text")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

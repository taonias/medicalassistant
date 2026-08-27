using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.UploadConsultationAudio;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.UploadConsultationDocument;
using MedicalAssistant.Application.MappingProfiles;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;

namespace MedicalAssistant.Application.UnitTests.Features;

public class UploadConsultationOutboxTests
{
    [Fact]
    public async Task Audio_upload_commits_consultation_and_outbox_without_request_thread_publish()
    {
        var consultation = new Consultation
        {
            Id = 42,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            Status = ConsultationStatus.Draft
        };
        var repository = new Mock<IConsultationFileRegistration>();
        repository
            .Setup(r => r.GetConsultationForDoctorAsync(42, "doctor-1"))
            .ReturnsAsync(consultation);
        var blobStorage = new Mock<IBlobStorageService>();
        blobStorage
            .Setup(s => s.UploadAsync(
                "consultation-audio",
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                "audio/wav",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("private://consultations/42/audio");
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetCurrentUserIdAsync()).ReturnsAsync("doctor-1");
        var mediator = new Mock<IMediator>();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ConsultationProfile>()).CreateMapper();
        var handler = new UploadConsultationAudioCommandHandler(
            repository.Object,
            blobStorage.Object,
            userService.Object,
            mapper,
            Options.Create(new BlobStorageSettings()));

        await handler.Handle(
            new UploadConsultationAudioCommand
            {
                ConsultationId = 42,
                AudioFile = FormFile("recording.wav", "audio/wav"),
                DurationSeconds = 30
            },
            CancellationToken.None);

        repository.Verify(r => r.UpdateWithOutboxAsync(
            consultation,
            It.Is<ConsultationOutboxMessage>(message =>
                message.EventType == "consultation.audio-uploaded.v1" &&
                message.EventVersion == 1 &&
                message.AggregateId == "42" &&
                message.Payload.Contains("\"consultationId\":42") &&
                !message.Payload.Contains("doctor-1") &&
                !message.Payload.Contains("patientId") &&
                !message.Payload.Contains("recording.wav")),
            It.IsAny<CancellationToken>()), Times.Once);
        // R28: IConsultationFileRegistration has no UpdateAsync member at all, so the
        // handler calling it instead of UpdateWithOutboxAsync is now a compile error,
        // not something this test needs to verify at runtime.
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Document_upload_commits_document_event_without_original_filename_or_identity()
    {
        var consultation = new Consultation
        {
            Id = 43,
            PatientId = 99,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            Status = ConsultationStatus.Draft
        };
        var repository = new Mock<IConsultationFileRegistration>();
        repository
            .Setup(r => r.GetConsultationForDoctorAsync(43, "doctor-1"))
            .ReturnsAsync(consultation);
        var blobStorage = new Mock<IBlobStorageService>();
        blobStorage
            .Setup(s => s.UploadAsync(
                "consultation-documents",
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                "application/pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("private://consultations/43/document");
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetCurrentUserIdAsync()).ReturnsAsync("doctor-1");
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ConsultationProfile>()).CreateMapper();
        var handler = new UploadConsultationDocumentCommandHandler(
            repository.Object,
            blobStorage.Object,
            userService.Object,
            mapper,
            Options.Create(new BlobStorageSettings()));

        await handler.Handle(
            new UploadConsultationDocumentCommand
            {
                ConsultationId = 43,
                DocumentFile = FormFile("patient-smith-referral.pdf", "application/pdf")
            },
            CancellationToken.None);

        Assert.Equal(ConsultationStatus.DocumentProcessingPending, consultation.Status);
        repository.Verify(r => r.UpdateWithOutboxAsync(
            consultation,
            It.Is<ConsultationOutboxMessage>(message =>
                message.EventType == "consultation.document-uploaded.v1" &&
                message.EventVersion == 1 &&
                message.AggregateId == "43" &&
                message.Payload.Contains("\"consultationId\":43") &&
                message.Payload.Contains("\"contentType\":\"application/pdf\"") &&
                !message.Payload.Contains("transcript", StringComparison.OrdinalIgnoreCase) &&
                !message.Payload.Contains("doctor-1") &&
                !message.Payload.Contains("patientId") &&
                !message.Payload.Contains("patient-smith-referral.pdf")),
            It.IsAny<CancellationToken>()), Times.Once);
        // R28: IConsultationFileRegistration has no UpdateAsync member at all, so the
        // handler calling it instead of UpdateWithOutboxAsync is now a compile error,
        // not something this test needs to verify at runtime.
    }

    private static IFormFile FormFile(string fileName, string contentType)
    {
        var bytes = new byte[] { 1, 2, 3 };
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}

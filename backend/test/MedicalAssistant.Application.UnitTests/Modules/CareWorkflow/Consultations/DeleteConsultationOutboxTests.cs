using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.DeleteConsultation;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Moq;

namespace MedicalAssistant.Application.UnitTests.Features;

public class DeleteConsultationOutboxTests
{
    [Fact]
    public async Task Deletion_records_tombstone_cleanup_and_deleted_event_without_queue_draining()
    {
        var consultation = new Consultation
        {
            Id = 8,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            AudioBlobUri = "private://consultations/8/audio"
        };
        var consultationDeletion = new Mock<IConsultationDeletion>();
        consultationDeletion
            .Setup(r => r.GetConsultationForDoctorAsync(8, "doctor-1"))
            .ReturnsAsync(consultation);
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetCurrentUserIdAsync()).ReturnsAsync("doctor-1");
        var handler = new DeleteConsultationCommandHandler(
            consultationDeletion.Object,
            userService.Object);

        await handler.Handle(new DeleteConsultationCommand(8), CancellationToken.None);

        Assert.NotNull(consultation.DeletedAtUtc);
        consultationDeletion.Verify(r => r.RecordDeletionAsync(
            consultation,
            It.Is<ConsultationDeletionCleanup>(cleanup =>
                cleanup.ConsultationId == 8 &&
                cleanup.BlobCleanupStatus == ConsultationCleanupStatus.Pending &&
                cleanup.ClinicalKnowledgeCleanupStatus == ConsultationCleanupStatus.Pending),
            It.Is<ConsultationOutboxMessage>(message =>
                message.EventType == "consultation.deleted.v1" &&
                message.Payload.Contains("\"consultationId\":8")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

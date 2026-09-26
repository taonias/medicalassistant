using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Features.MedicalStructuredData.Command.ProcessStructuredDataCallback;
using MedicalAssistant.Application.Notifications;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;
using Moq;
using Shouldly;

namespace MedicalAssistant.Application.UnitTests.Features;

public class ProcessStructuredDataCallbackCommandHandlerTests
{
    [Fact]
    public async Task Handle_persists_payload_and_marks_structured_data_pending()
    {
        var structured = new Mock<IMedicalStructuredDataRepository>();
        var consultations = new Mock<IConsultationRepository>();
        var mediator = new Mock<IMediator>();

        var consultation = new Consultation
        {
            Id = 42,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            Status = ConsultationStatus.Transcribed
        };

        consultations.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(consultation);
        structured.Setup(r => r.GetLatestByConsultationIdAsync(42)).ReturnsAsync((MedicalStructuredData?)null);
        structured.Setup(r => r.CreateAsync(It.IsAny<MedicalStructuredData>()))
            .ReturnsAsync((MedicalStructuredData entity) => entity);
        consultations.Setup(r => r.UpdateAsync(It.IsAny<Consultation>()))
            .ReturnsAsync((Consultation entity) => entity);

        var handler = new ProcessStructuredDataCallbackCommandHandler(
            structured.Object,
            consultations.Object,
            mediator.Object);

        await handler.Handle(new ProcessStructuredDataCallbackCommand
        {
            JobId = "corr-1",
            CorrelationId = "corr-1",
            ConsultationId = 42,
            TranscriptId = 10,
            SchemaVersion = "v1",
            StructuredPayload = """{"summary":"Dummy structured data for debugging."}""",
            Status = "completed"
        }, CancellationToken.None);

        consultation.Status.ShouldBe(ConsultationStatus.StructuredDataPending);
        structured.Verify(r => r.CreateAsync(It.Is<MedicalStructuredData>(d =>
            d.ConsultationId == 42 &&
            d.TranscriptId == 10 &&
            d.StructuredPayload.Contains("Dummy structured data"))), Times.Once);
        consultations.Verify(r => r.UpdateAsync(consultation), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<StructuredDataPersistedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_does_not_duplicate_existing_structured_data()
    {
        var structured = new Mock<IMedicalStructuredDataRepository>();
        var consultations = new Mock<IConsultationRepository>();
        var mediator = new Mock<IMediator>();

        var consultation = new Consultation
        {
            Id = 42,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            Status = ConsultationStatus.StructuredDataPending
        };

        structured.Setup(r => r.GetLatestByConsultationIdAsync(42))
            .ReturnsAsync(new MedicalStructuredData
            {
                ConsultationId = 42,
                SchemaVersion = "v1",
                StructuredPayload = """{"summary":"Existing"}"""
            });
        consultations.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(consultation);

        var handler = new ProcessStructuredDataCallbackCommandHandler(
            structured.Object,
            consultations.Object,
            mediator.Object);

        await handler.Handle(new ProcessStructuredDataCallbackCommand
        {
            JobId = "corr-1",
            CorrelationId = "corr-1",
            ConsultationId = 42,
            SchemaVersion = "v1",
            StructuredPayload = """{"summary":"Replacement"}""",
            Status = "completed"
        }, CancellationToken.None);

        structured.Verify(r => r.CreateAsync(It.IsAny<MedicalStructuredData>()), Times.Never);
        mediator.Verify(m => m.Publish(It.IsAny<StructuredDataPersistedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

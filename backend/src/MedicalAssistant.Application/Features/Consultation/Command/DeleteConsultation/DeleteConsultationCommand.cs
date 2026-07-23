using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Command.DeleteConsultation;

public record DeleteConsultationCommand(int Id) : IRequest<Unit>;

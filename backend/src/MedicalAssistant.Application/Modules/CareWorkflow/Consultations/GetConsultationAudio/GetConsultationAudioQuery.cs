using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationAudio;

public record GetConsultationAudioQuery(int ConsultationId) : IRequest<ConsultationAudioResult?>;

public class ConsultationAudioResult
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
}

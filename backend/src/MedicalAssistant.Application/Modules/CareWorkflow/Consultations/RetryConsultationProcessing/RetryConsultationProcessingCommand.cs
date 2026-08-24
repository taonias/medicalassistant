using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Command.RetryConsultationProcessing;

/// <summary>
/// Doctor-triggered manual retry of a failed consultation. Re-runs transcription when the
/// consultation is Failed, or clinical-knowledge indexing when the transcript succeeded but
/// indexing failed. Returns the refreshed consultation.
/// </summary>
public sealed class RetryConsultationProcessingCommand
    : IRequest<ConsultationDto>, IAuditableRequest<ConsultationDto>
{
    public int ConsultationId { get; set; }

    public AuditEntry ToAuditEntry(ConsultationDto response) =>
        new("RetryConsultationProcessing", "Consultation", ConsultationId.ToString());
}

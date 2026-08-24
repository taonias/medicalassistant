using MediatR;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationsByPatient;

namespace MedicalAssistant.Application.Features.Consultation.Queries.GetUnattachedDraftConsultations;

public record GetUnattachedDraftConsultationsQuery : IRequest<List<ConsultationSummaryDto>>;

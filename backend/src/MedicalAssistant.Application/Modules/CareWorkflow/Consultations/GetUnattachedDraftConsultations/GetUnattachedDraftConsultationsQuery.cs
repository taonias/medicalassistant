using MediatR;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationsByPatient;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetUnattachedDraftConsultations;

public record GetUnattachedDraftConsultationsQuery : IRequest<List<ConsultationSummaryDto>>;

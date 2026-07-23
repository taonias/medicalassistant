using MediatR;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;

namespace MedicalAssistant.Application.Features.Consultation.Command.AssignConsultationPatient;

public class AssignConsultationPatientCommand : IRequest<ConsultationDto>
{
    public int ConsultationId { get; set; }
    public int PatientId { get; set; }
}

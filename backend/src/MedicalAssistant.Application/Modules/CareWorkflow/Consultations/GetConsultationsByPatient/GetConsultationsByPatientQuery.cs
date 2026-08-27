using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationsByPatient;

public record GetConsultationsByPatientQuery(int PatientId) : IRequest<List<ConsultationSummaryDto>>;

public class ConsultationSummaryDto
{
    public int Id { get; set; }
    public DateTime ConsultationDate { get; set; }
    public required string Status { get; set; }
    public bool HasAudio { get; set; }
    public bool HasDocument { get; set; }
    public int? DurationSeconds { get; set; }
}

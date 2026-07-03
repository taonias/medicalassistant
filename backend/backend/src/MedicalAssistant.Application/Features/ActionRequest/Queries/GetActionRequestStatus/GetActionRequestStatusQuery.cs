using MediatR;

namespace MedicalAssistant.Application.Features.ActionRequest.Queries.GetActionRequestStatus;

public record GetActionRequestStatusQuery(string CorrelationId) : IRequest<ActionRequestDto>;

public class ActionRequestDto
{
    public int Id { get; set; }
    public required string CorrelationId { get; set; }
    public required string DoctorId { get; set; }
    public int? PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public required string ActionType { get; set; }
    public required string Status { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ExternalJobId { get; set; }
    public string? FailureReason { get; set; }
}

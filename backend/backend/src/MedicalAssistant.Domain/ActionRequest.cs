using MedicalAssistant.Domain.Common;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

public class ActionRequest : BaseEntity
{
    public required string CorrelationId { get; set; }
    public required string DoctorId { get; set; }
    public int? PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public ActionType ActionType { get; set; }
    public ActionRequestStatus Status { get; set; } = ActionRequestStatus.Pending;
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ExternalJobId { get; set; }
    public string? FailureReason { get; set; }

    public void MarkInProgress(string externalJobId)
    {
        Status = ActionRequestStatus.InProgress;
        ExternalJobId = externalJobId;
    }

    public void MarkCompleted(string responsePayload)
    {
        Status = ActionRequestStatus.Completed;
        ResponsePayload = responsePayload;
    }

    public void MarkFailed(string reason)
    {
        Status = ActionRequestStatus.Failed;
        FailureReason = reason;
    }
}

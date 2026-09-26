using MedicalAssistant.Domain.Common;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

public class ChatRequest : BaseEntity
{
    public required string CorrelationId { get; set; }
    public required string DoctorId { get; set; }
    public int? PatientId { get; set; }
    public required string Message { get; set; }
    public string? SessionId { get; set; }
    public string? ContextJson { get; set; }
    public ChatRequestStatus Status { get; set; } = ChatRequestStatus.Pending;
    public string? Answer { get; set; }
    public string? CitationsJson { get; set; }
    public string? SuggestedActionsJson { get; set; }
    public string? FailureReason { get; set; }

    public void MarkCompleted(string answer, string citationsJson, string suggestedActionsJson)
    {
        Status = ChatRequestStatus.Completed;
        Answer = answer;
        CitationsJson = citationsJson;
        SuggestedActionsJson = suggestedActionsJson;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = ChatRequestStatus.Failed;
        FailureReason = reason;
    }
}

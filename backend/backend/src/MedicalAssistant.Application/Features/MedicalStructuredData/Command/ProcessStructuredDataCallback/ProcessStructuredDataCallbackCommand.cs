using MediatR;

namespace MedicalAssistant.Application.Features.MedicalStructuredData.Command.ProcessStructuredDataCallback;

public class ProcessStructuredDataCallbackCommand : IRequest<Unit>
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public int ConsultationId { get; set; }
    public int? TranscriptId { get; set; }
    public required string SchemaVersion { get; set; }
    public required string StructuredPayload { get; set; }
    public required string Status { get; set; }
    public string? FailureReason { get; set; }
}

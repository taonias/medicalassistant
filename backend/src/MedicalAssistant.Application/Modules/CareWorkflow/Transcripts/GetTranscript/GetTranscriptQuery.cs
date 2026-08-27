using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Transcripts.GetTranscript;

public record GetTranscriptQuery(int ConsultationId) : IRequest<TranscriptDto?>;

public class TranscriptDto
{
    public int Id { get; set; }
    public int ConsultationId { get; set; }
    public required string Status { get; set; }
    public string? Transcript { get; set; }
    public string? ExternalJobId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? FailureReason { get; set; }
}

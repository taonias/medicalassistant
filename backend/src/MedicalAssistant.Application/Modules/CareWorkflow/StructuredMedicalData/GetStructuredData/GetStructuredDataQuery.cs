using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.StructuredMedicalData.GetStructuredData;

public record GetStructuredDataQuery(int ConsultationId) : IRequest<MedicalStructuredDataDto?>;

public class MedicalStructuredDataDto
{
    public int Id { get; set; }
    public int ConsultationId { get; set; }
    public int? TranscriptId { get; set; }
    public required string SchemaVersion { get; set; }
    public required string StructuredPayload { get; set; }
    public bool Approved { get; set; }
    public DateTime ExtractedAt { get; set; }
}

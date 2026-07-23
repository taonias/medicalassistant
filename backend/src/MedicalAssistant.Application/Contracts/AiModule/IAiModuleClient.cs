using MedicalAssistant.Application.Models.AiModule;

namespace MedicalAssistant.Application.Contracts.AiModule;

public interface IAiModuleClient
{
    Task<TranscriptionJobResponse> StartTranscriptionAsync(TranscriptionJobRequest request, CancellationToken cancellationToken = default);
    Task<TranscriptionStatusResponse> GetTranscriptionStatusAsync(string jobId, CancellationToken cancellationToken = default);
    Task<StructuredDataJobResponse> ExtractStructuredDataAsync(StructuredDataJobRequest request, CancellationToken cancellationToken = default);
    Task<IndexDocumentsResponse> IndexDocumentsAsync(IndexDocumentsJobRequest request, CancellationToken cancellationToken = default);
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
    Task<ActionJobResponse> TriggerActionAsync(ActionJobRequest request, CancellationToken cancellationToken = default);
    Task<ActionStatusResponse> GetActionStatusAsync(string jobId, CancellationToken cancellationToken = default);
}

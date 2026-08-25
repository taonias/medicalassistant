using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Transcription.Worker.Application.TranscribeAudio;

/// <summary>
/// What the Transcribe Audio workflow needs to do its work, assembled once at
/// the RabbitMQ/EventBus edge (R24) so the workflow itself never depends on
/// broker-specific types like <see cref="MedicalAssistant.EventBusRabbitMQ.RabbitMqTopologyOptions"/>.
/// </summary>
/// <param name="Envelope">
/// The original integration-event envelope. Carried through rather than
/// unpacked, because <c>ITranscriptionInboxStore</c> and
/// <c>ITranscriptionCompletionUnitOfWork</c> (owned by a different project,
/// out of scope for this change) key their idempotency and completion
/// contracts on the full envelope.
/// </param>
/// <param name="ConsumerName">
/// The resolved subscriber identity for this consumer — the RabbitMQ topology's
/// subscriber name, or a fallback, resolved once at the edge.
/// </param>
/// <param name="LeaseOwnerToken">A unique identifier for this specific processing attempt.</param>
internal sealed record TranscribeAudioWorkItem(
    IntegrationEventEnvelope<ConsultationAudioUploadedV1> Envelope,
    string ConsumerName,
    string LeaseOwnerToken);

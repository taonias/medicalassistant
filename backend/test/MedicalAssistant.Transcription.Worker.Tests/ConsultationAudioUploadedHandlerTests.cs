using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using MedicalAssistant.Transcription.Worker.Handlers;
using MedicalAssistant.Transcription.Worker.Options;
using MedicalAssistant.Transcription.Worker.Speech;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Tests;

public class ConsultationAudioUploadedHandlerTests
{
    [Fact]
    public async Task HandleAsync_retrieves_audio_transcribes_and_commits_completion_unit_of_work()
    {
        var audio = new ConsultationAudioBlob(new MemoryStream([1, 2, 3]), "audio/wav", 3);
        var retriever = new RecordingAudioRetriever(audio);
        var speech = new RecordingSpeechService(new SpeechTranscriptionResult("transcribed text", "en-US"));
        var unitOfWork = new RecordingCompletionUnitOfWork();
        var inboxStore = new RecordingInboxStore(TranscriptionInboxClaimStatus.Claimed);
        var handler = new ConsultationAudioUploadedIntegrationEventHandler(
            inboxStore,
            retriever,
            speech,
            unitOfWork,
            Microsoft.Extensions.Options.Options.Create(new RabbitMqTopologyOptions { SubscriberName = "transcription-worker" }),
            Microsoft.Extensions.Options.Options.Create(new TranscriptionWorkerOptions { ProcessingLeaseDuration = TimeSpan.FromMinutes(10) }),
            NullLogger<ConsultationAudioUploadedIntegrationEventHandler>.Instance);
        var envelope = new IntegrationEventEnvelope<ConsultationAudioUploadedV1>(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ConsultationIntegrationEvents.AudioUploadedV1,
            1,
            DateTime.UtcNow,
            "medicalassistant.backend",
            "correlation-1",
            null,
            new ConsultationAudioUploadedV1(
                42,
                "file-42",
                "audio/wav",
                "private://consultations/42/audio.wav",
                3));

        await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal("private://consultations/42/audio.wav", retriever.StorageObjectReference);
        Assert.Equal(envelope.EventId, inboxStore.Envelope!.EventId);
        Assert.Same(audio, speech.Audio);
        Assert.NotNull(unitOfWork.Request);
        Assert.Equal("transcription-worker", unitOfWork.Request.ConsumerName);
        Assert.Equal(envelope, unitOfWork.Request.Envelope);
        Assert.Equal("transcribed text", unitOfWork.Request.TranscriptText);
        Assert.Equal("en-US", unitOfWork.Request.LanguageCode);
    }

    [Fact]
    public async Task HandleAsync_skips_blob_and_speech_when_inbox_event_is_already_completed()
    {
        var retriever = new RecordingAudioRetriever(
            new ConsultationAudioBlob(new MemoryStream([1]), "audio/wav", 1));
        var speech = new RecordingSpeechService(new SpeechTranscriptionResult("should not run", "en-US"));
        var unitOfWork = new RecordingCompletionUnitOfWork();
        var handler = new ConsultationAudioUploadedIntegrationEventHandler(
            new RecordingInboxStore(TranscriptionInboxClaimStatus.DuplicateCompleted),
            retriever,
            speech,
            unitOfWork,
            Microsoft.Extensions.Options.Options.Create(new RabbitMqTopologyOptions { SubscriberName = "transcription-worker" }),
            Microsoft.Extensions.Options.Options.Create(new TranscriptionWorkerOptions { ProcessingLeaseDuration = TimeSpan.FromMinutes(10) }),
            NullLogger<ConsultationAudioUploadedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(CreateEnvelope(), CancellationToken.None);

        Assert.Null(retriever.StorageObjectReference);
        Assert.Null(speech.Audio);
        Assert.Null(unitOfWork.Request);
    }

    [Theory]
    [InlineData(TranscriptionInboxClaimStatus.SkippedDeleted)]
    [InlineData(TranscriptionInboxClaimStatus.SkippedSuperseded)]
    public async Task HandleAsync_skips_blob_and_speech_when_state_gate_skips_event(
        TranscriptionInboxClaimStatus skippedStatus)
    {
        var retriever = new RecordingAudioRetriever(
            new ConsultationAudioBlob(new MemoryStream([1]), "audio/wav", 1));
        var speech = new RecordingSpeechService(new SpeechTranscriptionResult("should not run", "en-US"));
        var unitOfWork = new RecordingCompletionUnitOfWork();
        var handler = new ConsultationAudioUploadedIntegrationEventHandler(
            new RecordingInboxStore(skippedStatus),
            retriever,
            speech,
            unitOfWork,
            Microsoft.Extensions.Options.Options.Create(new RabbitMqTopologyOptions { SubscriberName = "transcription-worker" }),
            Microsoft.Extensions.Options.Options.Create(new TranscriptionWorkerOptions { ProcessingLeaseDuration = TimeSpan.FromMinutes(10) }),
            NullLogger<ConsultationAudioUploadedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(CreateEnvelope(), CancellationToken.None);

        Assert.Null(retriever.StorageObjectReference);
        Assert.Null(speech.Audio);
        Assert.Null(unitOfWork.Request);
        Assert.Null(unitOfWork.FailureRequest);
    }


    [Fact]
    public async Task HandleAsync_commits_transcription_failed_for_permanent_speech_failure()
    {
        var retriever = new RecordingAudioRetriever(
            new ConsultationAudioBlob(new MemoryStream([1]), "audio/wav", 1));
        var speech = new FailingSpeechService(new SpeechTranscriptionFailureException(
            SpeechTranscriptionFailureCategory.Permanent,
            "speech-unsupported-audio",
            "Azure Speech rejected the audio format."));
        var unitOfWork = new RecordingCompletionUnitOfWork();
        var handler = new ConsultationAudioUploadedIntegrationEventHandler(
            new RecordingInboxStore(TranscriptionInboxClaimStatus.Claimed),
            retriever,
            speech,
            unitOfWork,
            Microsoft.Extensions.Options.Options.Create(new RabbitMqTopologyOptions { SubscriberName = "transcription-worker" }),
            Microsoft.Extensions.Options.Options.Create(new TranscriptionWorkerOptions { ProcessingLeaseDuration = TimeSpan.FromMinutes(10) }),
            NullLogger<ConsultationAudioUploadedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(CreateEnvelope(), CancellationToken.None);

        Assert.Null(unitOfWork.Request);
        Assert.NotNull(unitOfWork.FailureRequest);
        Assert.Equal("speech-unsupported-audio", unitOfWork.FailureRequest.FailureCode);
        Assert.Equal("Permanent", unitOfWork.FailureRequest.FailureCategory);
    }

    private sealed class RecordingAudioRetriever : IConsultationAudioBlobRetriever
    {
        private readonly ConsultationAudioBlob _audio;

        public RecordingAudioRetriever(ConsultationAudioBlob audio)
        {
            _audio = audio;
        }

        public string? StorageObjectReference { get; private set; }

        public Task<ConsultationAudioBlob> OpenReadAsync(
            string storageObjectReference,
            CancellationToken cancellationToken = default)
        {
            StorageObjectReference = storageObjectReference;
            return Task.FromResult(_audio);
        }
    }

    private sealed class RecordingInboxStore : ITranscriptionInboxStore
    {
        private readonly TranscriptionInboxClaimStatus _status;

        public RecordingInboxStore(TranscriptionInboxClaimStatus status)
        {
            _status = status;
        }

        public IntegrationEventEnvelope<ConsultationAudioUploadedV1>? Envelope { get; private set; }

        public Task<TranscriptionInboxClaimResult> ClaimAsync(
            string consumerName,
            IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
            string leaseOwner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            Envelope = envelope;
            return Task.FromResult(new TranscriptionInboxClaimResult(_status));
        }
    }

    private sealed class RecordingSpeechService : ISpeechTranscriptionService
    {
        private readonly SpeechTranscriptionResult _result;

        public RecordingSpeechService(SpeechTranscriptionResult result)
        {
            _result = result;
        }

        public ConsultationAudioBlob? Audio { get; private set; }

        public Task<SpeechTranscriptionResult> TranscribeAsync(
            ConsultationAudioBlob audio,
            CancellationToken cancellationToken = default)
        {
            Audio = audio;
            return Task.FromResult(_result);
        }
    }

    private sealed class FailingSpeechService : ISpeechTranscriptionService
    {
        private readonly SpeechTranscriptionFailureException _exception;

        public FailingSpeechService(SpeechTranscriptionFailureException exception)
        {
            _exception = exception;
        }

        public Task<SpeechTranscriptionResult> TranscribeAsync(
            ConsultationAudioBlob audio,
            CancellationToken cancellationToken = default) =>
            Task.FromException<SpeechTranscriptionResult>(_exception);
    }

    private sealed class RecordingCompletionUnitOfWork : ITranscriptionCompletionUnitOfWork
    {
        public TranscriptionCompletionRequest? Request { get; private set; }
        public TranscriptionFailureRequest? FailureRequest { get; private set; }

        public Task<TranscriptionCompletionResult> CompleteAsync(
            TranscriptionCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new TranscriptionCompletionResult(
                TranscriptionCompletionStatus.Completed,
                request.Envelope.Payload.ConsultationId,
                TranscriptId: 99));
        }

        public Task<TranscriptionFailureResult> FailAsync(
            TranscriptionFailureRequest request,
            CancellationToken cancellationToken = default)
        {
            FailureRequest = request;
            return Task.FromResult(new TranscriptionFailureResult(
                TranscriptionFailureStatus.Failed,
                request.Envelope.Payload.ConsultationId));
        }
    }

    private static IntegrationEventEnvelope<ConsultationAudioUploadedV1> CreateEnvelope()
    {
        return new IntegrationEventEnvelope<ConsultationAudioUploadedV1>(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ConsultationIntegrationEvents.AudioUploadedV1,
            1,
            DateTime.UtcNow,
            "medicalassistant.backend",
            "correlation-1",
            null,
            new ConsultationAudioUploadedV1(
                42,
                "file-42",
                "audio/wav",
                "private://consultations/42/audio.wav",
                3));
    }
}

using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using MedicalAssistant.Transcription.Worker.Handlers;
using MedicalAssistant.Transcription.Worker.Speech;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class ConsultationAudioUploadedHandlerTests
{
    [Fact]
    public async Task HandleAsync_retrieves_audio_transcribes_and_commits_completion_unit_of_work()
    {
        var audio = new ConsultationAudioBlob(new MemoryStream([1, 2, 3]), "audio/wav", 3);
        var retriever = new RecordingAudioRetriever(audio);
        var speech = new RecordingSpeechService(new SpeechTranscriptionResult("transcribed text", "en-US"));
        var unitOfWork = new RecordingCompletionUnitOfWork();
        var handler = new ConsultationAudioUploadedIntegrationEventHandler(
            retriever,
            speech,
            unitOfWork,
            Options.Create(new RabbitMqTopologyOptions { SubscriberName = "transcription-worker" }),
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
        Assert.Same(audio, speech.Audio);
        Assert.NotNull(unitOfWork.Request);
        Assert.Equal("transcription-worker", unitOfWork.Request.ConsumerName);
        Assert.Equal(envelope, unitOfWork.Request.Envelope);
        Assert.Equal("transcribed text", unitOfWork.Request.TranscriptText);
        Assert.Equal("en-US", unitOfWork.Request.LanguageCode);
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

    private sealed class RecordingCompletionUnitOfWork : ITranscriptionCompletionUnitOfWork
    {
        public TranscriptionCompletionRequest? Request { get; private set; }

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
    }
}

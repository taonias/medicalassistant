using MedicalAssistant.Transcription.Worker.Storage;

namespace MedicalAssistant.Transcription.Worker.Speech;

public interface ISpeechTranscriptionService
{
    Task<SpeechTranscriptionResult> TranscribeAsync(
        ConsultationAudioBlob audio,
        CancellationToken cancellationToken = default);
}

public sealed record SpeechTranscriptionResult(
    string TranscriptText,
    string LanguageCode);

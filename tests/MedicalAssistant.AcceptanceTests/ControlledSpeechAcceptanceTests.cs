namespace MedicalAssistant.AcceptanceTests;

public sealed class ControlledSpeechAcceptanceTests
{
    [Fact]
    public async Task Scripted_speech_adapter_returns_the_next_transcript()
    {
        var speech = new ControlledSpeechAdapter();
        speech.EnqueueTranscript("Synthetic transcript from the controlled adapter.");

        var transcript = await speech.TranscribeAsync(SyntheticRecording.Wave("SPEECH-CANARY"));

        Assert.Equal("Synthetic transcript from the controlled adapter.", transcript);
    }

    [Fact]
    public async Task Scripted_speech_adapter_can_hold_and_release_processing()
    {
        var speech = new ControlledSpeechAdapter();
        var release = speech.EnqueueBlockedTranscript("Released synthetic transcript.");

        var transcription = speech.TranscribeAsync(SyntheticRecording.Wave("BLOCKED-SPEECH-CANARY"));
        Assert.False(transcription.IsCompleted);

        release();
        Assert.Equal("Released synthetic transcript.", await transcription);
    }
}

namespace MedicalAssistant.AcceptanceTests;

public sealed class BackendUploadAcceptanceTests
{
    [Fact]
    public async Task Doctor_uploads_synthetic_audio_through_the_backend_http_interface()
    {
        await using var environment = new AcceptanceEnvironment();
        await environment.StartAsync();
        var doctor = await environment.CreateDoctorClientAsync();

        var consultation = await doctor.CreateConsultationAsync();
        var recording = SyntheticRecording.Wave("T02-SYNTHETIC-AUDIO-CANARY");

        var uploaded = await doctor.UploadAudioAsync(consultation.Id, recording);
        var observed = await doctor.GetConsultationAsync(consultation.Id);

        Assert.Equal("AudioUploaded", uploaded.Status);
        Assert.Equal("AudioUploaded", observed.Status);
        Assert.Equal(recording.ContentType, observed.AudioContentType);
        Assert.True(environment.BlobStorage.Contains(observed.AudioBlobUri));
    }
}

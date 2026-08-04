namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class LegacyRemovalCompletionTests
{
    [Fact]
    public void Removed_function_and_direct_publisher_paths_are_not_active_repository_files()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenRelativePaths = new[]
        {
            "transcriber/MedicalAssistant.Transcriber.csproj",
            "transcriber/Program.cs",
            "transcriber/host.json",
            "transcriber/local.settings.json.example",
            "transcriber/Functions/ProcessConsultationFileFunction.cs",
            "transcriber/Services/RabbitMqTranscriptReadyPublisher.cs",
            "backend/src/MedicalAssistant.Infrastructure/Messaging/RabbitMqConsultationProcessingPublisher.cs",
            "backend/src/MedicalAssistant.Infrastructure/Messaging/RabbitMqTranscriptReadyPublisher.cs",
            "backend/src/MedicalAssistant.Application/Contracts/Messaging/IConsultationProcessingPublisher.cs",
            "backend/src/MedicalAssistant.Application/Contracts/Messaging/ITranscriptReadyPublisher.cs",
            "backend/src/MedicalAssistant.Application/Models/Messaging/ConsultationProcessingMessage.cs",
            "backend/src/MedicalAssistant.Application/Models/Messaging/TranscriptReadyMessage.cs",
            "backend/src/MedicalAssistant.Application/Models/RabbitMqSettings.cs"
        };

        var existing = forbiddenRelativePaths
            .Select(path => Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))
            .Where(File.Exists)
            .ToArray();

        Assert.Empty(existing);
    }

    [Fact]
    public void Replacement_worker_contains_the_ported_blob_and_speech_adapter_seams()
    {
        var repositoryRoot = FindRepositoryRoot();
        var requiredRelativePaths = new[]
        {
            "backend/src/MedicalAssistant.Transcription.Worker/Speech/AzureSpeechTranscriptionService.cs",
            "backend/src/MedicalAssistant.Transcription.Worker/Speech/ISpeechTranscriptionService.cs",
            "backend/src/MedicalAssistant.Transcription.Worker/Storage/ConsultationAudioBlobRetriever.cs",
            "backend/src/MedicalAssistant.Transcription.Worker/Storage/IConsultationAudioBlobRetriever.cs",
            "backend/src/MedicalAssistant.Transcription.Worker/Storage/AzurePrivateBlobObjectClient.cs",
            "backend/src/MedicalAssistant.Transcription.Worker/Handlers/ConsultationAudioUploadedIntegrationEventHandler.cs"
        };

        var missing = requiredRelativePaths
            .Select(path => Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))
            .Where(path => !File.Exists(path))
            .ToArray();

        Assert.Empty(missing);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend", "src")) &&
                File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the medicalassistant repository root.");
    }
}

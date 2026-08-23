using System.Diagnostics;

namespace MedicalAssistant.AcceptanceTests;

/// <summary>Runs the production Transcription Worker as its own process.</summary>
public sealed class TranscriptionWorkerServiceControl : IServiceLifecycleControl, IAsyncDisposable
{
    private readonly Func<string> _databaseConnectionString;
    private readonly Func<Uri> _rabbitMqEndpoint;
    private readonly Func<string> _blobConnectionString;
    private readonly Func<Uri> _providerEndpoint;
    private Process? _process;

    internal TranscriptionWorkerServiceControl(
        Func<string> databaseConnectionString,
        Func<Uri> rabbitMqEndpoint,
        Func<string> blobConnectionString,
        Func<Uri> providerEndpoint)
    {
        _databaseConnectionString = databaseConnectionString;
        _rabbitMqEndpoint = rabbitMqEndpoint;
        _blobConnectionString = blobConnectionString;
        _providerEndpoint = providerEndpoint;
    }

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        await StopAsync(CancellationToken.None);
        var rabbitMq = _rabbitMqEndpoint();
        var start = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(LocateWorkerAssembly());
        start.Environment["DOTNET_ENVIRONMENT"] = "Acceptance";
        start.Environment["Database__Provider"] = "PostgreSQL";
        start.Environment["ConnectionStrings__MedicalAssistantDatabasePostgreSQL"] = _databaseConnectionString();
        start.Environment["RabbitMQ__HostName"] = rabbitMq.Host;
        start.Environment["RabbitMQ__Port"] = rabbitMq.Port.ToString();
        start.Environment["RabbitMQ__VirtualHost"] = "/";
        start.Environment["RabbitMQ__UserName"] = Uri.UnescapeDataString(rabbitMq.UserInfo.Split(':')[0]);
        start.Environment["RabbitMQ__Password"] = Uri.UnescapeDataString(rabbitMq.UserInfo.Split(':')[1]);
        start.Environment["RabbitMQ__ClientProvidedName"] = "acceptance-transcription-worker";
        start.Environment["RabbitMQ__Consumer__QueueName"] = "medicalassistant.transcription-worker";
        start.Environment["RabbitMQ__Topology__SubscriberName"] = "transcription-worker";
        start.Environment["RabbitMQ__Topology__QueueName"] = "medicalassistant.transcription-worker";
        start.Environment["TranscriptionWorker__Provider"] = "OpenAiWhisper";
        start.Environment["TranscriptionWorker__ShutdownDrainTimeout"] = "00:00:05";
        start.Environment["TranscriptionWorker__ProcessingLeaseDuration"] = "00:01:00";
        start.Environment["TranscriptionWorker__BlobRetrieval__ConnectionString"] = _blobConnectionString();
        start.Environment["TranscriptionWorker__BlobRetrieval__ConsultationAudioContainer"] = "acceptance-audio";
        start.Environment["TranscriptionWorker__OpenAiWhisper__ApiKey"] = "controlled-provider-key";
        start.Environment["TranscriptionWorker__OpenAiWhisper__BaseUrl"] =
            new Uri(_providerEndpoint(), "/v1").ToString().TrimEnd('/');
        start.Environment["TranscriptionWorker__OpenAiWhisper__Model"] = "controlled-transcription";

        _process = Process.Start(start)
            ?? throw new InvalidOperationException("The Transcription Worker process could not be started.");
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await Task.Delay(250, cancellationToken);
        if (_process.HasExited)
            throw new InvalidOperationException("The Transcription Worker exited during startup.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
            return;
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        await _process.WaitForExitAsync(CancellationToken.None);
        _process.Dispose();
        _process = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private static string LocateWorkerAssembly()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "backend",
                "src",
                "MedicalAssistant.Transcription.Worker",
                "bin",
                configuration,
                "net8.0",
                "MedicalAssistant.Transcription.Worker.dll");
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("Build the Transcription Worker before running acceptance tests.");
    }
}

using System.Diagnostics;

namespace MedicalAssistant.AcceptanceTests;

/// <summary>
/// Drives the packaged services from the repository's root Compose definition.
/// This is deliberately separate from <see cref="AcceptanceEnvironment"/>:
/// Compose validates Dockerfiles, networks, dependency conditions, environment
/// mapping, and named-volume behavior that an in-process host cannot cover.
/// </summary>
internal sealed class RootComposeEnvironment : IAsyncDisposable
{
    private const string ComposeProjectName = "medicalassistant-r06";
    private readonly string _repositoryRoot = LocateRepositoryRoot();
    private readonly ControlledProviderService _providers;
    private bool _composeStarted;

    public RootComposeEnvironment()
    {
        _providers = new ControlledProviderService(Speech, Models);
    }

    public ControlledSpeechAdapter Speech { get; } = new();

    public ControlledModelAdapter Models { get; } = new();

    public async Task StartWithEmptyVolumesAsync(CancellationToken cancellationToken = default)
    {
        await _providers.StartAsync(cancellationToken);
        await ComposeDownAsync(deleteVolumes: true, cancellationToken);
        await ComposeUpAsync(cancellationToken);
    }

    public async Task RestartWithExistingVolumesAsync(CancellationToken cancellationToken = default)
    {
        if (!_composeStarted)
            throw new InvalidOperationException("Start root Compose before restarting it.");
        await ComposeDownAsync(deleteVolumes: false, cancellationToken);
        await ComposeUpAsync(cancellationToken);
    }

    public Task<DoctorApiClient> CreateDoctorClientAsync(CancellationToken cancellationToken = default) =>
        DoctorApiClient.RegisterAsync(
            new HttpClient { BaseAddress = new Uri("http://127.0.0.1:7037") },
            cancellationToken);

    public ClinicalKnowledgeApiClient CreateClinicalKnowledgeClient() =>
        new(
            new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8000") },
            "local-development-key");

    public async ValueTask DisposeAsync()
    {
        Exception? cleanupError = null;
        try
        {
            await ComposeDownAsync(deleteVolumes: true, CancellationToken.None);
        }
        catch (Exception exception)
        {
            cleanupError = exception;
        }
        await _providers.DisposeAsync();
        if (cleanupError is not null)
            throw cleanupError;
    }

    private async Task ComposeUpAsync(CancellationToken cancellationToken)
    {
        await RunComposeAsync(
            [
                "up",
                "--detach",
                "--build",
                "--wait",
                "--wait-timeout", "360",
                "backend-api",
                "transcription-worker",
            ],
            cancellationToken);
        _composeStarted = true;
    }

    private async Task ComposeDownAsync(bool deleteVolumes, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "down", "--remove-orphans" };
        if (deleteVolumes)
            arguments.Add("--volumes");
        await RunComposeAsync(arguments, cancellationToken);
        _composeStarted = false;
    }

    private async Task RunComposeAsync(
        IReadOnlyCollection<string> commandArguments,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("compose");
        start.ArgumentList.Add("--project-name");
        start.ArgumentList.Add(ComposeProjectName);
        start.ArgumentList.Add("--file");
        start.ArgumentList.Add(Path.Combine(_repositoryRoot, "docker-compose.yml"));
        start.ArgumentList.Add("--file");
        start.ArgumentList.Add(Path.Combine(
            _repositoryRoot,
            "tests",
            "MedicalAssistant.AcceptanceTests",
            "docker-compose.acceptance.yml"));
        foreach (var argument in commandArguments)
            start.ArgumentList.Add(argument);
        start.Environment["CONTROLLED_PROVIDER_PORT"] = _providers.Endpoint.Port.ToString();

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Docker Compose could not be started.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker Compose exited with code {process.ExitCode}.\n{output}\n{error}");
        }
    }

    private static string LocateRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the root docker-compose.yml.");
    }
}

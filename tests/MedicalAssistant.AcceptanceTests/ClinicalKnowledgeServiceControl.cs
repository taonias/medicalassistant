using System.Diagnostics;
using System.Net;

namespace MedicalAssistant.AcceptanceTests;

public interface IServiceLifecycleControl
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs Clinical Knowledge out-of-process so crash/restart scenarios cross its
/// real HTTP and process boundaries while keeping provider data synthetic.
/// </summary>
public sealed class ClinicalKnowledgeServiceControl : IServiceLifecycleControl, IAsyncDisposable
{
    private readonly Func<string> _connectionString;
    private readonly string _apiKey;
    private Process? _process;

    internal ClinicalKnowledgeServiceControl(Func<string> connectionString, string apiKey)
    {
        _connectionString = connectionString;
        _apiKey = apiKey;
    }

    public Uri Endpoint { get; private set; } = null!;

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        if (_process is not null)
        {
            _process.Dispose();
            _process = null;
        }

        var assemblyPath = LocateClinicalKnowledgeAssembly();
        var endpointReady = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(assemblyPath);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add("http://127.0.0.1:0");
        start.Environment["ConnectionStrings__Postgres"] = _connectionString();
        start.Environment["Authentication__ApiKeys__0"] = _apiKey;
        start.Environment["Ingestion__WorkerCount"] = "0";
        start.Environment["DOTNET_ENVIRONMENT"] = "Acceptance";

        try
        {
            _process = Process.Start(start)
                ?? throw new InvalidOperationException("Clinical Knowledge process could not be started.");
            _process.EnableRaisingEvents = true;
            _process.OutputDataReceived += (_, args) =>
            {
                if (TryReadListeningEndpoint(args.Data, out var endpoint))
                    endpointReady.TrySetResult(endpoint);
            };
            _process.ErrorDataReceived += (_, _) => { };
            _process.Exited += (_, _) => endpointReady.TrySetException(
                new InvalidOperationException("Clinical Knowledge exited during startup."));
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            Endpoint = await endpointReady.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            await WaitUntilReadyAsync(cancellationToken);
        }
        catch
        {
            await StopAsync(CancellationToken.None);
            throw;
        }
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

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = Endpoint,
            Timeout = TimeSpan.FromSeconds(1),
        };
        client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync($"/ingestions/{Guid.Empty}", cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException
                && !cancellationToken.IsCancellationRequested)
            {
            }
            await Task.Delay(100, cancellationToken);
        }
        await StopAsync(cancellationToken);
        throw new TimeoutException("Clinical Knowledge did not become ready within 30 seconds.");
    }

    private static bool TryReadListeningEndpoint(string? output, out Uri endpoint)
    {
        const string marker = "Now listening on:";
        var markerIndex = output?.IndexOf(marker, StringComparison.OrdinalIgnoreCase) ?? -1;
        if (markerIndex >= 0
            && Uri.TryCreate(output![(markerIndex + marker.Length)..].Trim(), UriKind.Absolute, out var parsed))
        {
            endpoint = parsed;
            return true;
        }
        endpoint = null!;
        return false;
    }

    private static string LocateClinicalKnowledgeAssembly()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "AI",
                "src",
                "MedicalAssistance.Ingestion.Api",
                "bin",
                configuration,
                "net10.0",
                "MedicalAssistance.Ingestion.Api.dll");
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("Build the Clinical Knowledge API before running acceptance tests.");
    }
}

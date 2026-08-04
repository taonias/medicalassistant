using System.Text.RegularExpressions;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class ArchitecturePrivacyEnforcementTests
{
    private static readonly Regex LegacyQueueNamePattern = new(
        @"(?<![A-Za-z0-9.-])consultation\.(processing|transcript)(?![A-Za-z0-9.-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FunctionArtifactPattern = new(
        @"Microsoft\.Azure\.Functions|ConfigureFunctionsWorkerDefaults|\[Function(?:Name)?\]|\[RabbitMQTrigger\]|AzureWebJobs|FUNCTIONS_WORKER_RUNTIME",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReflectionRoutingPattern = new(
        @"typeof\s*\([^)]*\)\s*\.Name",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void Active_source_and_config_do_not_reintroduce_functions_or_legacy_direct_queues()
    {
        var matches = ActiveSourceAndConfigFiles()
            .SelectMany(path => FindMatches(
                path,
                [
                    ("Azure Functions artifact", FunctionArtifactPattern),
                    ("legacy direct queue", LegacyQueueNamePattern)
                ]))
            .ToArray();

        Assert.Empty(matches);
    }

    [Fact]
    public void Integration_events_do_not_use_default_exchange_or_reflection_based_routing()
    {
        var sourceFiles = ActiveCSharpSourceFiles().ToArray();
        var reflectionRoutingMatches = sourceFiles
            .Where(path =>
                path.Contains($"{Path.DirectorySeparatorChar}MedicalAssistant.EventBus", StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}MedicalAssistant.EventBusRabbitMQ", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => FindMatches(path, [("reflection routing", ReflectionRoutingPattern)]));
        var defaultExchangeMatches = sourceFiles
            .Where(path => !path.EndsWith(
                Path.Combine("MedicalAssistant.EventBusRabbitMQ", "RabbitMqHostedConsumer.cs"),
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { line, index })
                .Where(item => item.line.Contains("exchange: string.Empty", StringComparison.Ordinal))
                .Select(item => $"{path}:{item.index + 1}: default exchange publish"));

        Assert.Empty(reflectionRoutingMatches.Concat(defaultExchangeMatches));
    }

    [Fact]
    public void Application_hosts_do_not_apply_database_migrations_or_drain_queues()
    {
        var repositoryRoot = FindRepositoryRoot();
        var hostFiles = new[]
        {
            Path.Combine(repositoryRoot, "backend", "src", "MedicalAssistant.Api", "Program.cs"),
            Path.Combine(repositoryRoot, "backend", "src", "MedicalAssistant.Transcription.Worker", "Program.cs")
        };

        foreach (var hostFile in hostFiles)
        {
            var text = File.ReadAllText(hostFile);
            Assert.DoesNotContain("Database.Migrate", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MigrateAsync", text, StringComparison.Ordinal);
            Assert.DoesNotContain("EnsureCreated", text, StringComparison.Ordinal);
        }

        var queueDrainingMatches = ActiveCSharpSourceFiles()
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { line, index })
                .Where(item =>
                    item.line.Contains("BasicGetAsync", StringComparison.Ordinal) ||
                    item.line.Contains("QueuePurge", StringComparison.Ordinal) ||
                    item.line.Contains("QueueDelete", StringComparison.Ordinal))
                .Select(item => $"{path}:{item.index + 1}: queue-draining API"));

        Assert.Empty(queueDrainingMatches);
    }

    [Fact]
    public void Event_processing_logs_and_telemetry_do_not_use_payload_or_phi_canary_fields()
    {
        var forbiddenTelemetryTerms = new[]
        {
            "TranscriptText",
            "StorageObjectReference",
            "BlobUri",
            "FileName",
            "Payload",
            "responseBody",
            "provider body",
            "transcript preview",
            "signed url",
            "AccountKey"
        };
        var telemetryFiles = ActiveCSharpSourceFiles()
            .Where(path =>
                path.Contains($"{Path.DirectorySeparatorChar}MedicalAssistant.EventBus", StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}MedicalAssistant.EventBusRabbitMQ", StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}MedicalAssistant.Transcription.Worker", StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}EventHandlers{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}ConsultationOutbox", StringComparison.OrdinalIgnoreCase));

        var violations = telemetryFiles
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { line, index })
                .Where(item => item.line.Contains(".Log", StringComparison.Ordinal) ||
                               item.line.Contains("Meter", StringComparison.Ordinal) ||
                               item.line.Contains("Counter", StringComparison.Ordinal) ||
                               item.line.Contains("Histogram", StringComparison.Ordinal))
                .SelectMany(item => forbiddenTelemetryTerms
                    .Where(term => item.line.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Select(term => $"{path}:{item.index + 1}: telemetry includes forbidden term '{term}'")))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Repository_no_longer_contains_active_host_json_or_function_local_settings_files()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenFiles = Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(IsInActiveTree)
            .Where(path =>
                string.Equals(Path.GetFileName(path), "host.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(path), "local.settings.json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(forbiddenFiles);
    }

    private static IEnumerable<string> ActiveSourceAndConfigFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var activeRoots = new[]
        {
            Path.Combine(repositoryRoot, "backend", "src"),
            Path.Combine(repositoryRoot, "AI", "src")
        };
        var sourceFiles = activeRoots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        var rootConfigFiles = new[]
        {
            Path.Combine(repositoryRoot, "docker-compose.yml"),
            Path.Combine(repositoryRoot, "compose.env.example")
        }.Where(File.Exists);

        return sourceFiles.Concat(rootConfigFiles)
            .Where(IsSupportedActiveFile)
            .Where(IsInActiveTree);
    }

    private static IEnumerable<string> ActiveCSharpSourceFiles() =>
        ActiveSourceAndConfigFiles()
            .Where(path => string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> FindMatches(
        string path,
        IReadOnlyList<(string Label, Regex Pattern)> policies)
    {
        return File.ReadLines(path)
            .Select((line, index) => new { line, index })
            .SelectMany(item => policies
                .Where(policy => policy.Pattern.IsMatch(item.line))
                .Select(policy => $"{path}:{item.index + 1}: {policy.Label}: {item.line.Trim()}"));
    }

    private static bool IsSupportedActiveFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetFileName(path), "Dockerfile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInActiveTree(string path)
    {
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var separator = Path.DirectorySeparatorChar;
        var ignoredSegments = new[]
        {
            $"{separator}bin{separator}",
            $"{separator}obj{separator}",
            $"{separator}.scratch{separator}",
            $"{separator}project-docs{separator}",
            $"{separator}docs{separator}",
            $"{separator}Contracts{separator}Examples{separator}"
        };

        return !ignoredSegments.Any(segment => normalized.Contains(segment, StringComparison.OrdinalIgnoreCase));
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

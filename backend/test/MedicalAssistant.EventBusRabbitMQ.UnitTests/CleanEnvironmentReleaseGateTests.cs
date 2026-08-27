namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class CleanEnvironmentReleaseGateTests
{
    [Fact]
    public void Release_gate_document_covers_clean_environment_operations_and_stop_conditions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documentPath = Path.Combine(
            repositoryRoot,
            "project-docs",
            "event bus implementations",
            "14-clean-environment-release-gate.md");

        Assert.True(File.Exists(documentPath), $"Missing release gate document: {documentPath}");

        var document = File.ReadAllText(documentPath);
        var requiredTerms = new[]
        {
            "Stop release",
            "disposable environment",
            "empty volumes",
            "docker compose -f docker-compose.yml --env-file",
            "backend-migrations",
            "backend-api",
            "transcription-worker",
            "clinical-knowledge",
            "rabbitmq-provisioner",
            "dotnet test backend/MedicalAssistant.slnx --no-restore",
            "dotnet build backend/MedicalAssistant.slnx --no-restore",
            "ArchitecturePrivacyEnforcementTests",
            "FailureMatrixCoverageTests",
            "LegacyRemovalCompletionTests",
            "dashboard",
            "alert",
            "backup",
            "restore",
            "no-Functions scan",
            "release evidence"
        };

        foreach (var term in requiredTerms)
        {
            Assert.Contains(term, document, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("TBD", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TODO", document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Release_gate_document_tracks_the_root_compose_deployment_units()
    {
        var repositoryRoot = FindRepositoryRoot();
        var composePath = Path.Combine(repositoryRoot, "docker-compose.yml");
        var documentPath = Path.Combine(
            repositoryRoot,
            "project-docs",
            "event bus implementations",
            "14-clean-environment-release-gate.md");

        var compose = File.ReadAllText(composePath);
        var document = File.ReadAllText(documentPath);
        var services = new[]
        {
            "postgres",
            "rabbitmq",
            "azurite",
            "backend-migrations",
            "backend-api",
            "transcription-worker",
            "clinical-knowledge",
            "rabbitmq-provisioner"
        };

        foreach (var service in services)
        {
            Assert.Contains($"{service}:", compose, StringComparison.Ordinal);
            Assert.Contains(service, document, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Release_gate_is_discoverable_from_the_event_bus_document_index()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readmePath = Path.Combine(
            repositoryRoot,
            "project-docs",
            "event bus implementations",
            "README.md");
        var readme = File.ReadAllText(readmePath);

        Assert.Contains("14-clean-environment-release-gate.md", readme, StringComparison.Ordinal);
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

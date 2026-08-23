using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Mvc.Testing;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;

namespace MedicalAssistant.AcceptanceTests;

/// <summary>
/// The pre-Compose acceptance seam used while Consultation Processing is built.
/// Product behavior is observed through HTTP clients; only infrastructure and
/// external-provider adapters are controllable by tests.
/// </summary>
public sealed class AcceptanceEnvironment : IAsyncDisposable
{
    private const string RabbitMqUser = "acceptance";
    private const string RabbitMqPassword = "acceptance";
    private const string AzuriteAccount = "acceptance";
    private const string AzuriteKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    internal const string ClinicalKnowledgeApiKey = "acceptance-clinical-knowledge-key";

    private readonly PostgreSqlContainer _backendDatabase =
        new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("medical_assistant_acceptance")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private readonly PostgreSqlContainer _clinicalKnowledgeDatabase =
        new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("clinical_knowledge_acceptance")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private readonly IContainer _rabbitMq = new ContainerBuilder("rabbitmq:3.13-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", RabbitMqUser)
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", RabbitMqPassword)
        .WithPortBinding(5672, true)
        .WithPortBinding(15672, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
        .Build();

    private readonly IContainer _azurite = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
        .WithEnvironment("AZURITE_ACCOUNTS", $"{AzuriteAccount}:{AzuriteKey}")
        .WithCommand(
            "azurite",
            "--blobHost", "0.0.0.0",
            "--skipApiVersionCheck",
            "--loose")
        .WithPortBinding(10000, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(10000))
        .Build();

    private bool _started;
    private bool _fullSystem;
    private bool _azuriteStarted;
    private bool _backendDatabaseMigrated;
    private BackendApiFactory? _backendApi;
    private readonly ControlledProviderService _providers;

    public AcceptanceEnvironment()
    {
        Broker = new BrokerControl(_rabbitMq, () => RabbitMqUri);
        ClinicalKnowledge = new ClinicalKnowledgeServiceControl(
            () => ClinicalKnowledgeDatabaseConnectionString,
            ClinicalKnowledgeApiKey);
        _providers = new ControlledProviderService(Speech, Models);
        Worker = new TranscriptionWorkerServiceControl(
            () => BackendDatabaseConnectionString,
            () => RabbitMqUri,
            () => AzuriteConnectionString,
            () => _providers.Endpoint);
    }

    public BrokerControl Broker { get; }

    public ClinicalKnowledgeServiceControl ClinicalKnowledge { get; }

    public ControlledBlobStorage BlobStorage { get; } = new();

    public ControlledSpeechAdapter Speech { get; } = new();

    public ControlledModelAdapter Models { get; } = new();

    public TranscriptionWorkerServiceControl Worker { get; }

    public string BackendDatabaseConnectionString => RequireStarted(_backendDatabase.GetConnectionString());

    public string ClinicalKnowledgeDatabaseConnectionString =>
        RequireStarted(_clinicalKnowledgeDatabase.GetConnectionString());

    public Uri RabbitMqUri => new(
        $"amqp://{RabbitMqUser}:{RabbitMqPassword}@{RequireStarted(_rabbitMq.Hostname)}:{_rabbitMq.GetMappedPublicPort(5672)}");

    public string AzuriteConnectionString =>
        $"DefaultEndpointsProtocol=http;AccountName={AzuriteAccount};AccountKey={AzuriteKey};" +
        $"BlobEndpoint=http://{RequireAzuriteStarted(_azurite.Hostname)}:{_azurite.GetMappedPublicPort(10000)}/{AzuriteAccount};";

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        await Task.WhenAll(
            _backendDatabase.StartAsync(cancellationToken),
            _clinicalKnowledgeDatabase.StartAsync(cancellationToken),
            _rabbitMq.StartAsync(cancellationToken));
        _started = true;
    }

    /// <summary>
    /// Starts the same asynchronous service boundaries as the root Compose
    /// stack while routing only true provider calls to deterministic adapters.
    /// </summary>
    public async Task StartFullSystemAsync(CancellationToken cancellationToken = default)
    {
        if (_fullSystem && Worker.IsRunning && ClinicalKnowledge.IsRunning)
            return;

        await StartAsync(cancellationToken);
        if (!_azuriteStarted)
        {
            await _azurite.StartAsync(cancellationToken);
            _azuriteStarted = true;
        }
        await _providers.StartAsync(cancellationToken);
        ClinicalKnowledge.UseControlledProviders(() => _providers.Endpoint);
        _fullSystem = true;
        await StartApplicationsAsync(cancellationToken);
    }

    public async Task StopApplicationsAsync(CancellationToken cancellationToken = default)
    {
        if (_backendApi is not null)
        {
            await _backendApi.DisposeAsync();
            _backendApi = null;
        }
        await Worker.StopAsync(cancellationToken);
        await ClinicalKnowledge.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Restarts every application and infrastructure container without deleting
    /// their data, matching a Compose restart against existing named volumes.
    /// </summary>
    public async Task RestartFullSystemAsync(CancellationToken cancellationToken = default)
    {
        if (!_fullSystem)
            throw new InvalidOperationException("Start the full system before restarting it.");

        await StopApplicationsAsync(cancellationToken);
        await Task.WhenAll(
            _azurite.StopAsync(cancellationToken),
            _rabbitMq.StopAsync(cancellationToken),
            _clinicalKnowledgeDatabase.StopAsync(cancellationToken),
            _backendDatabase.StopAsync(cancellationToken));
        await Task.WhenAll(
            _backendDatabase.StartAsync(cancellationToken),
            _clinicalKnowledgeDatabase.StartAsync(cancellationToken),
            _rabbitMq.StartAsync(cancellationToken),
            _azurite.StartAsync(cancellationToken));
        await StartApplicationsAsync(cancellationToken);
    }

    public async Task StartApplicationsAsync(CancellationToken cancellationToken = default)
    {
        if (!_fullSystem)
            throw new InvalidOperationException("Start the full system before restarting its applications.");

        await EnsureBackendDatabaseMigratedAsync(cancellationToken);
        await ClinicalKnowledge.StartAsync(cancellationToken);
        _backendApi ??= new BackendApiFactory(
            BackendDatabaseConnectionString,
            RabbitMqUri,
            AzuriteConnectionString,
            ClinicalKnowledge.Endpoint);
        await Worker.StartAsync(cancellationToken);
    }

    public async Task<DoctorApiClient> CreateDoctorClientAsync(CancellationToken cancellationToken = default)
    {
        _ = RequireStarted(BackendDatabaseConnectionString);
        await EnsureBackendDatabaseMigratedAsync(cancellationToken);
        _backendApi ??= new BackendApiFactory(
            BackendDatabaseConnectionString,
            RabbitMqUri,
            BlobStorage);
        var http = _backendApi.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        return await DoctorApiClient.RegisterAsync(http, cancellationToken);
    }

    public async Task<ClinicalKnowledgeApiClient> CreateClinicalKnowledgeClientAsync(
        CancellationToken cancellationToken = default)
    {
        _ = RequireStarted(ClinicalKnowledgeDatabaseConnectionString);
        await ClinicalKnowledge.StartAsync(cancellationToken);
        return new ClinicalKnowledgeApiClient(
            new HttpClient { BaseAddress = ClinicalKnowledge.Endpoint },
            ClinicalKnowledgeApiKey);
    }

    public async ValueTask DisposeAsync()
    {
        var errors = new List<Exception>();
        await CaptureAsync(async () =>
        {
            if (_backendApi is not null)
                await _backendApi.DisposeAsync();
        });
        await CaptureAsync(async () => await Worker.DisposeAsync());
        await CaptureAsync(async () => await ClinicalKnowledge.DisposeAsync());
        await CaptureAsync(async () => await _providers.DisposeAsync());
        await CaptureAsync(async () => await _azurite.DisposeAsync());
        await CaptureAsync(async () => await _rabbitMq.DisposeAsync());
        await CaptureAsync(async () => await _clinicalKnowledgeDatabase.DisposeAsync());
        await CaptureAsync(async () => await _backendDatabase.DisposeAsync());

        if (errors.Count > 0)
            throw new AggregateException("Acceptance environment cleanup failed.", errors);

        async Task CaptureAsync(Func<Task> dispose)
        {
            try
            {
                await dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
    }

    private string RequireStarted(string value)
    {
        if (!_started)
            throw new InvalidOperationException("Start the acceptance environment before reading its endpoints.");
        return value;
    }

    private string RequireAzuriteStarted(string value)
    {
        if (!_azuriteStarted)
            throw new InvalidOperationException("Start the full acceptance system before reading Blob storage settings.");
        return value;
    }

    private async Task EnsureBackendDatabaseMigratedAsync(CancellationToken cancellationToken)
    {
        if (_backendDatabaseMigrated)
            return;
        await BackendDatabaseMigrator.MigrateAsync(BackendDatabaseConnectionString, cancellationToken);
        _backendDatabaseMigrated = true;
    }
}

/// <summary>Creates deterministic RabbitMQ outages without exposing container internals to scenarios.</summary>
public sealed class BrokerControl
{
    private readonly IContainer _container;
    private readonly Func<Uri> _endpoint;

    internal BrokerControl(IContainer container, Func<Uri> endpoint)
    {
        _container = container;
        _endpoint = endpoint;
    }

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _container.StopAsync(cancellationToken);

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _container.StartAsync(cancellationToken);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                Uri = _endpoint(),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(1),
            };
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection.IsOpen;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }
}

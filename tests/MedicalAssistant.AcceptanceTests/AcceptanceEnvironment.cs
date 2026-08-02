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
    internal const string ClinicalKnowledgeApiKey = "acceptance-clinical-knowledge-key";

    private readonly PostgreSqlContainer _backendDatabase =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("medical_assistant_acceptance")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private readonly PostgreSqlContainer _clinicalKnowledgeDatabase =
        new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("clinical_knowledge_acceptance")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private readonly IContainer _rabbitMq = new ContainerBuilder("rabbitmq:4-management-alpine")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", RabbitMqUser)
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", RabbitMqPassword)
        .WithPortBinding(5672, true)
        .WithPortBinding(15672, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
        .Build();

    private bool _started;
    private bool _backendDatabaseMigrated;
    private BackendApiFactory? _backendApi;

    public AcceptanceEnvironment()
    {
        Broker = new BrokerControl(_rabbitMq, () => RabbitMqUri);
        ClinicalKnowledge = new ClinicalKnowledgeServiceControl(
            () => ClinicalKnowledgeDatabaseConnectionString,
            ClinicalKnowledgeApiKey);
    }

    public BrokerControl Broker { get; }

    public ClinicalKnowledgeServiceControl ClinicalKnowledge { get; }

    public ControlledBlobStorage BlobStorage { get; } = new();

    public ControlledSpeechAdapter Speech { get; } = new();

    public string BackendDatabaseConnectionString => RequireStarted(_backendDatabase.GetConnectionString());

    public string ClinicalKnowledgeDatabaseConnectionString =>
        RequireStarted(_clinicalKnowledgeDatabase.GetConnectionString());

    public Uri RabbitMqUri => new(
        $"amqp://{RabbitMqUser}:{RabbitMqPassword}@{RequireStarted(_rabbitMq.Hostname)}:{_rabbitMq.GetMappedPublicPort(5672)}");

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

    public async Task<DoctorApiClient> CreateDoctorClientAsync(CancellationToken cancellationToken = default)
    {
        _ = RequireStarted(BackendDatabaseConnectionString);
        if (!_backendDatabaseMigrated)
        {
            await BackendDatabaseMigrator.MigrateAsync(BackendDatabaseConnectionString, cancellationToken);
            _backendDatabaseMigrated = true;
        }
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
        await CaptureAsync(async () => await ClinicalKnowledge.DisposeAsync());
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

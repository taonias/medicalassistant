using MedicalAssistant.Application.Contracts.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedicalAssistant.AcceptanceTests;

internal sealed class BackendApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseConnectionString;
    private readonly Uri _rabbitMqUri;
    private readonly ControlledBlobStorage? _controlledBlobStorage;
    private readonly string? _blobConnectionString;
    private readonly Uri? _clinicalKnowledgeEndpoint;

    public BackendApiFactory(
        string databaseConnectionString,
        Uri rabbitMqUri,
        ControlledBlobStorage blobStorage)
    {
        _databaseConnectionString = databaseConnectionString;
        _rabbitMqUri = rabbitMqUri;
        _controlledBlobStorage = blobStorage;
    }

    public BackendApiFactory(
        string databaseConnectionString,
        Uri rabbitMqUri,
        string blobConnectionString,
        Uri clinicalKnowledgeEndpoint)
    {
        _databaseConnectionString = databaseConnectionString;
        _rabbitMqUri = rabbitMqUri;
        _blobConnectionString = blobConnectionString;
        _clinicalKnowledgeEndpoint = clinicalKnowledgeEndpoint;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Acceptance");
        builder.UseSetting("https_port", "443");
        builder.UseSetting("Database:Provider", "PostgreSQL");
        builder.UseSetting("ConnectionStrings:MedicalAssistantDatabasePostgreSQL", _databaseConnectionString);
        builder.UseSetting("RabbitMQ:HostName", _rabbitMqUri.Host);
        builder.UseSetting("RabbitMQ:Port", _rabbitMqUri.Port.ToString());
        builder.UseSetting("RabbitMQ:VirtualHost", "/");
        builder.UseSetting("RabbitMQ:UserName", Uri.UnescapeDataString(_rabbitMqUri.UserInfo.Split(':')[0]));
        builder.UseSetting("RabbitMQ:Password", Uri.UnescapeDataString(_rabbitMqUri.UserInfo.Split(':')[1]));
        builder.UseSetting("RabbitMQ:ClientProvidedName", "acceptance-backend");
        builder.UseSetting("RabbitMQ:Consumer:QueueName", "medicalassistant.backend.transcript-ready");
        builder.UseSetting("RabbitMQ:Topology:SubscriberName", "backend-clinical-knowledge");
        builder.UseSetting("RabbitMQ:Topology:QueueName", "medicalassistant.backend.transcript-ready");
        builder.UseSetting("ConsultationOutboxRelay:Enabled", (_controlledBlobStorage is null).ToString());
        builder.UseSetting("ConsultationOutboxRelay:PollInterval", "00:00:00.100");
        builder.UseSetting("ConsultationOutboxRelay:FailureBackoff", "00:00:00.100");
        builder.UseSetting("BlobStorage:ConsultationAudioContainer", "acceptance-audio");
        builder.UseSetting("BlobStorage:ConsultationDocumentsContainer", "acceptance-documents");
        if (_blobConnectionString is not null)
            builder.UseSetting("BlobStorage:ConnectionString", _blobConnectionString);
        if (_clinicalKnowledgeEndpoint is not null)
        {
            builder.UseSetting("ClinicalKnowledge:BaseUrl", _clinicalKnowledgeEndpoint.ToString());
            builder.UseSetting("ClinicalKnowledge:ApiKey", AcceptanceEnvironment.ClinicalKnowledgeApiKey);
        }
        if (_controlledBlobStorage is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBlobStorageService>();
                services.AddSingleton<IBlobStorageService>(_controlledBlobStorage);
            });
        }
    }
}

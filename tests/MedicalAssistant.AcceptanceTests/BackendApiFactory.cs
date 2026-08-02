using MedicalAssistant.Application.Contracts.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedicalAssistant.AcceptanceTests;

internal sealed class BackendApiFactory(
    string databaseConnectionString,
    Uri rabbitMqUri,
    ControlledBlobStorage blobStorage) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Acceptance");
        builder.UseSetting("https_port", "443");
        builder.UseSetting("Database:Provider", "PostgreSQL");
        builder.UseSetting("ConnectionStrings:MedicalAssistantDatabasePostgreSQL", databaseConnectionString);
        builder.UseSetting("RabbitMq:Host", rabbitMqUri.Host);
        builder.UseSetting("RabbitMq:Port", rabbitMqUri.Port.ToString());
        builder.UseSetting("RabbitMq:VirtualHost", "/");
        builder.UseSetting("RabbitMq:Username", Uri.UnescapeDataString(rabbitMqUri.UserInfo.Split(':')[0]));
        builder.UseSetting("RabbitMq:Password", Uri.UnescapeDataString(rabbitMqUri.UserInfo.Split(':')[1]));
        // The legacy direct-to-queue publisher must stay disabled. Later event-bus
        // tasks connect the transactional outbox to this harness.
        builder.UseSetting("RabbitMq:Enabled", "false");
        builder.UseSetting("BlobStorage:ConsultationAudioContainer", "acceptance-audio");
        builder.UseSetting("BlobStorage:ConsultationDocumentsContainer", "acceptance-documents");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService>(blobStorage);
        });
    }
}

using MedicalAssistant.EventBusRabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class RabbitMqConnectionSecurityOptionsTests
{
    [Fact]
    public void Connection_options_bind_existing_hostname_username_keys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:HostName"] = "rabbitmq.internal",
                ["RabbitMQ:Port"] = "5671",
                ["RabbitMQ:UserName"] = "backend-clinical-knowledge",
                ["RabbitMQ:Password"] = "backend-secret",
                ["RabbitMQ:VirtualHost"] = "/medicalassistant",
                ["RabbitMQ:ClientProvidedName"] = "backend-clinical-knowledge",
                ["RabbitMQ:UseTls"] = "true",
                ["RabbitMQ:TlsServerName"] = "broker.internal"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddRabbitMqEventBusConsumer(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>().Value;

        Assert.Equal("rabbitmq.internal", options.Host);
        Assert.Equal("backend-clinical-knowledge", options.Username);
        Assert.True(options.UseTls);
    }

    [Fact]
    public void Connection_options_reject_shared_default_broker_credentials()
    {
        var validator = new RabbitMqConnectionOptionsValidator();
        var options = new RabbitMqConnectionOptions
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest",
            ClientProvidedName = "backend-clinical-knowledge"
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("workload-specific service identity"));
        Assert.Contains(result.Failures, failure => failure.Contains("shared default development password"));
    }

    [Fact]
    public void Connection_factory_applies_workload_identity_and_tls_settings()
    {
        var options = new RabbitMqConnectionOptions
        {
            HostName = "rabbitmq.internal",
            Port = 5671,
            VirtualHost = "/medicalassistant",
            UserName = "transcription-worker",
            Password = "worker-secret",
            UseTls = true,
            TlsServerName = "broker.internal"
        };

        var factory = RabbitMqConnectionFactoryBuilder.Create(options);

        Assert.Equal("rabbitmq.internal", factory.HostName);
        Assert.Equal(5671, factory.Port);
        Assert.Equal("/medicalassistant", factory.VirtualHost);
        Assert.Equal("transcription-worker", factory.UserName);
        Assert.True(factory.Ssl.Enabled);
        Assert.Equal("broker.internal", factory.Ssl.ServerName);
    }
}

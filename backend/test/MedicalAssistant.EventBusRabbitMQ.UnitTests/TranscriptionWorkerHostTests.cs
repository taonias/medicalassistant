using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using MedicalAssistant.Transcription.Worker;
using MedicalAssistant.Transcription.Worker.Handlers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class TranscriptionWorkerHostTests
{
    [Fact]
    public void Worker_registration_binds_only_audio_uploaded_to_a_standard_rabbitmq_consumer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Topology:SubscriberName"] = "transcription-worker",
                ["RabbitMQ:Topology:QueueName"] = "medicalassistant.transcription-worker",
                ["RabbitMQ:Consumer:QueueName"] = "medicalassistant.transcription-worker",
                ["RabbitMQ:Consumer:PrefetchCount"] = "1"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddTranscriptionWorkerServices(configuration);

        using var provider = services.BuildServiceProvider();
        var subscriptions = provider.GetRequiredService<IntegrationEventSubscriptionRegistry>();

        var subscription = Assert.Single(subscriptions.Subscriptions);
        Assert.Equal(ConsultationIntegrationEvents.AudioUploadedV1, subscription.Contract.EventType);
        Assert.Equal(typeof(ConsultationAudioUploadedIntegrationEventHandler), subscription.HandlerType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                          descriptor.ImplementationType == typeof(RabbitMqHostedConsumer));
    }

    [Fact]
    public void Worker_assembly_has_no_azure_functions_runtime_dependency()
    {
        var referencedAssemblies = typeof(TranscriptionWorkerServiceRegistration)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain(referencedAssemblies, name =>
            name is not null &&
            name.Contains("Functions", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblies, name =>
            name is not null &&
            name.Contains("WebJobs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Worker_registration_uses_transcription_concurrency_to_bound_rabbitmq_prefetch_and_shutdown()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Topology:SubscriberName"] = "transcription-worker",
                ["RabbitMQ:Topology:QueueName"] = "medicalassistant.transcription-worker",
                ["RabbitMQ:Consumer:QueueName"] = "medicalassistant.transcription-worker",
                ["RabbitMQ:Consumer:PrefetchCount"] = "99",
                ["RabbitMQ:Consumer:ShutdownDrainTimeout"] = "00:00:30",
                ["TranscriptionWorker:MaxConcurrentTranscriptions"] = "3",
                ["TranscriptionWorker:ShutdownDrainTimeout"] = "00:00:07"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddTranscriptionWorkerServices(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMqConsumerOptions>>().Value;

        Assert.Equal((ushort)3, options.PrefetchCount);
        Assert.Equal(TimeSpan.FromSeconds(7), options.ShutdownDrainTimeout);
    }
}

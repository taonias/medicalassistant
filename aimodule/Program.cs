using MedicalAssistant.AiModule.Options;
using MedicalAssistant.AiModule.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<RabbitMqOptions>(options =>
        {
            options.Connection = context.Configuration["RabbitMqConnection"]
                ?? context.Configuration["RabbitMq:Connection"];
            options.AiResultsQueue =
                context.Configuration["RabbitMqResultsQueueName"]
                ?? context.Configuration["RabbitMq:AiResultsQueue"]
                ?? "ai.results";
        });

        services.AddSingleton<IAiResultPublisher, RabbitMqAiResultPublisher>();
    })
    .Build();

host.Run();

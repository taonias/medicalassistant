using MedicalAssistant.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Services;

public sealed class ConsultationOutboxRelayHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConsultationOutboxRelayOptions _options;

    public ConsultationOutboxRelayHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ConsultationOutboxRelayOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var relay = scope.ServiceProvider.GetRequiredService<ConsultationOutboxRelay>();
            await relay.ProcessDueBatchAsync(stoppingToken);
            await Task.Delay(_options.PollInterval, stoppingToken);
        }
    }
}

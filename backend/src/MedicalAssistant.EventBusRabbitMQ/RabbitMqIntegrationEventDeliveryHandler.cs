using System.Text.Json;
using MedicalAssistant.EventBus;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqIntegrationEventDeliveryHandler : IRabbitMqDeliveryHandler
{
    private readonly IntegrationEventDispatcher _dispatcher;

    public RabbitMqIntegrationEventDeliveryHandler(IntegrationEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<RabbitMqDeliveryOutcome> HandleAsync(
        RabbitMqDelivery delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dispatcher.DispatchAsync(
                delivery.EventType,
                delivery.EventVersion,
                delivery.EnvelopeJson,
                cancellationToken);

            return RabbitMqDeliveryOutcome.Acknowledge;
        }
        catch (UnsupportedIntegrationEventContractException)
        {
            return RabbitMqDeliveryOutcome.DeadLetter;
        }
        catch (JsonException)
        {
            return RabbitMqDeliveryOutcome.DeadLetter;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return RabbitMqDeliveryOutcome.Retry;
        }
    }
}

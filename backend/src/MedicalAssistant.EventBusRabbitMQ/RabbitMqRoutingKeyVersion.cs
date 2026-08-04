namespace MedicalAssistant.EventBusRabbitMQ;

public static class RabbitMqRoutingKeyVersion
{
    public static int Parse(string routingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        var versionStart = routingKey.LastIndexOf(".v", StringComparison.Ordinal);
        if (versionStart < 0 || versionStart + 2 >= routingKey.Length)
        {
            return 1;
        }

        return int.TryParse(routingKey[(versionStart + 2)..], out var version)
            ? version
            : 1;
    }
}

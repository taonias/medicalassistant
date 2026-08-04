using Microsoft.Extensions.Options;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqConnectionOptionsValidator : IValidateOptions<RabbitMqConnectionOptions>
{
    private static readonly string[] ReservedUsernames = ["guest", "admin", "administrator"];
    private static readonly string[] ReservedPasswords = ["guest", "admin", "password", "p@ssw0rd"];

    public ValidateOptionsResult Validate(string? name, RabbitMqConnectionOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add("RabbitMQ:HostName is required.");
        }

        if (options.Port <= 0)
        {
            failures.Add("RabbitMQ:Port must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.VirtualHost))
        {
            failures.Add("RabbitMQ:VirtualHost is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            failures.Add("RabbitMQ:UserName is required and must identify the current workload.");
        }
        else if (ReservedUsernames.Contains(options.Username.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            failures.Add("RabbitMQ:UserName must be a workload-specific service identity, not a shared broker administrator/default user.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            failures.Add("RabbitMQ:Password is required and must come from the secret store/environment.");
        }
        else if (ReservedPasswords.Contains(options.Password.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            failures.Add("RabbitMQ:Password must not use a shared default development password.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientProvidedName))
        {
            failures.Add("RabbitMQ:ClientProvidedName is required for broker auditability.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

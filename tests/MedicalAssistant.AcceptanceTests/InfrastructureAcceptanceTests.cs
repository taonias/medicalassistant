using Npgsql;
using RabbitMQ.Client;

namespace MedicalAssistant.AcceptanceTests;

public sealed class InfrastructureAcceptanceTests
{
    [Fact]
    public async Task Environment_exposes_real_postgres_and_rabbitmq()
    {
        await using var environment = new AcceptanceEnvironment();

        await environment.StartAsync();

        await using var database = new NpgsqlConnection(environment.BackendDatabaseConnectionString);
        await database.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT 1", database);
        Assert.Equal(1, await command.ExecuteScalarAsync());

        var factory = new ConnectionFactory { Uri = environment.RabbitMqUri };
        await using var broker = await factory.CreateConnectionAsync();
        Assert.True(broker.IsOpen);
    }

    [Fact]
    public async Task Broker_control_can_create_and_recover_an_outage()
    {
        await using var environment = new AcceptanceEnvironment();
        await environment.StartAsync();

        Assert.True(await environment.Broker.IsAvailableAsync());

        await environment.Broker.StopAsync();
        Assert.False(await environment.Broker.IsAvailableAsync());

        await environment.Broker.StartAsync();
        Assert.True(await environment.Broker.IsAvailableAsync());
    }
}

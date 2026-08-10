using MedicalAssistant.Identity;
using MedicalAssistant.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["JwtSettings:Key"] = "migration-runner-placeholder-key-with-enough-length",
    ["JwtSettings:Issuer"] = "medical-assistant-migrations",
    ["JwtSettings:Audience"] = "medical-assistant-migrations"
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);

var host = builder.Build();

await PersistenceDbInitializer.MigrateAsync(host.Services);
await IdentityDbMigrator.MigrateAsync(host.Services);
await IdentityDbInitializer.SeedRolesAsync(host.Services);
await IdentityDbInitializer.SeedDevelopmentDoctorAsync(host.Services);

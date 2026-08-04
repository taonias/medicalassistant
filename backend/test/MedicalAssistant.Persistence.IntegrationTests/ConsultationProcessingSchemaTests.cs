using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.IntegrationTests;

public class ConsultationProcessingSchemaTests
{
    [Fact]
    public void Model_contains_durable_event_and_deletion_schema()
    {
        var options = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>()
            .UseNpgsql("Host=localhost;Database=medicalassistant_schema_test;Username=test;Password=test")
            .Options;

        using var context = new MedicalAssistantDatabaseContext(options, new HttpContextAccessor());
        var model = context.Model;

        Assert.NotNull(model.FindEntityType("MedicalAssistant.Domain.ConsultationOutboxMessage"));
        Assert.NotNull(model.FindEntityType("MedicalAssistant.Domain.ConsultationInboxMessage"));
        Assert.NotNull(model.FindEntityType("MedicalAssistant.Domain.ConsultationDeletionCleanup"));

        Assert.NotNull(model.FindEntityType("MedicalAssistant.Domain.Transcript")!
            .FindProperty("Revision"));
        Assert.NotNull(model.FindEntityType("MedicalAssistant.Domain.Transcript")!
            .FindProperty("ConcurrencyToken"));

        Assert.NotNull(model.FindEntityType("MedicalAssistant.Domain.Consultation")!
            .FindProperty("SourceObjectReference"));
        Assert.NotNull(model.FindEntityType("MedicalAssistant.Domain.Consultation")!
            .FindProperty("DeletedAtUtc"));
    }
}

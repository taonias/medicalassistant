using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

public class ConsultationInboxMessageConfiguration : IEntityTypeConfiguration<ConsultationInboxMessage>
{
    public void Configure(EntityTypeBuilder<ConsultationInboxMessage> builder)
    {
        builder.ToTable("ConsultationInboxMessages");
        builder.Property(m => m.ConsumerName).HasMaxLength(200).IsRequired();
        builder.Property(m => m.EventType).HasMaxLength(200).IsRequired();
        builder.Property(m => m.LeaseOwner).HasMaxLength(200);
        builder.Property(m => m.LastFailureCategory).HasMaxLength(100);
        builder.Property(m => m.LastFailureCode).HasMaxLength(200);
        builder.Property(m => m.ClinicalKnowledgeDocumentId).HasMaxLength(512);
        builder.HasIndex(m => new { m.ConsumerName, m.EventId }).IsUnique();
        builder.HasIndex(m => new { m.ConsumerName, m.Status, m.LeaseExpiresAtUtc });
    }
}

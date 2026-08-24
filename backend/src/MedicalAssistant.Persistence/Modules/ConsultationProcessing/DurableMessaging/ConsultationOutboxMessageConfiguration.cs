using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

public class ConsultationOutboxMessageConfiguration : IEntityTypeConfiguration<ConsultationOutboxMessage>
{
    public void Configure(EntityTypeBuilder<ConsultationOutboxMessage> builder)
    {
        builder.ToTable("ConsultationOutboxMessages");
        builder.Property(m => m.EventType).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Producer).HasMaxLength(100).IsRequired();
        builder.Property(m => m.CorrelationId).HasMaxLength(100);
        builder.Property(m => m.CausationId).HasMaxLength(100);
        builder.Property(m => m.AggregateType).HasMaxLength(100);
        builder.Property(m => m.AggregateId).HasMaxLength(100);
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LeaseOwner).HasMaxLength(200);
        builder.Property(m => m.LastFailureCategory).HasMaxLength(100);
        builder.Property(m => m.LastFailureCode).HasMaxLength(200);
        builder.HasIndex(m => m.EventId).IsUnique();
        builder.HasIndex(m => new { m.Status, m.NextAttemptAtUtc, m.LeaseExpiresAtUtc });
        builder.HasIndex(m => new { m.EventType, m.EventVersion });
    }
}

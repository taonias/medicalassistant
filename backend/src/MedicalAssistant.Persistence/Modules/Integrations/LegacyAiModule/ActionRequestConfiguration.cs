using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

public class ActionRequestConfiguration : IEntityTypeConfiguration<ActionRequest>
{
    public void Configure(EntityTypeBuilder<ActionRequest> builder)
    {
        builder.ToTable("ActionRequests");
        builder.Property(a => a.CorrelationId).HasMaxLength(64).IsRequired();
        builder.Property(a => a.DoctorId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.RequestPayload).HasColumnType("jsonb");
        builder.Property(a => a.ResponsePayload).HasColumnType("jsonb");
        builder.Property(a => a.ExternalJobId).HasMaxLength(128);
        builder.Property(a => a.FailureReason).HasMaxLength(2000);
        builder.HasIndex(a => a.CorrelationId).IsUnique();
    }
}

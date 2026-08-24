using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

public class TranscriptConfiguration : IEntityTypeConfiguration<Transcript>
{
    public void Configure(EntityTypeBuilder<Transcript> builder)
    {
        builder.ToTable("Transcripts");
        builder.Property(t => t.TranscriptText)
            .HasColumnName("Transcript")
            .HasColumnType("text");
        builder.Property(t => t.ExternalJobId).HasMaxLength(128);
        builder.Property(t => t.FailureReason).HasMaxLength(2000);
        builder.Property(t => t.Revision).HasDefaultValue(1);
        builder.Property(t => t.ConcurrencyToken)
            .HasDefaultValueSql("gen_random_uuid()")
            .IsConcurrencyToken();
        builder.HasIndex(t => t.ConsultationId).IsUnique();
        builder.HasIndex(t => t.ExternalJobId);
    }
}

using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Modules.CareWorkflow.Consultations;

public class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> builder)
    {
        builder.ToTable("Consultations");
        builder.Property(c => c.DoctorId).HasMaxLength(450).IsRequired();
        builder.Property(c => c.AudioBlobUri).HasMaxLength(2048);
        builder.Property(c => c.AudioContentType).HasMaxLength(100);
        builder.Property(c => c.DocumentBlobUri).HasMaxLength(2048);
        builder.Property(c => c.DocumentContentType).HasMaxLength(100);
        builder.Property(c => c.DocumentFileName).HasMaxLength(512);
        builder.Property(c => c.IdempotencyKey).HasMaxLength(128);
        builder.Property(c => c.FailureReason).HasMaxLength(2000);
        builder.Property(c => c.SourceObjectReference).HasMaxLength(2048);
        builder.Property(c => c.SourceObjectETag).HasMaxLength(256);
        builder.Property(c => c.DeletedBy).HasMaxLength(450);
        builder.Property(c => c.DeletionReasonCode).HasMaxLength(100);
        builder.HasIndex(c => new { c.DoctorId, c.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasIndex(c => c.SourceObjectReference)
            .HasFilter("\"SourceObjectReference\" IS NOT NULL");
        builder.HasIndex(c => c.DeletedAtUtc)
            .HasFilter("\"DeletedAtUtc\" IS NOT NULL");
        builder.HasOne<Patient>().WithMany().HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

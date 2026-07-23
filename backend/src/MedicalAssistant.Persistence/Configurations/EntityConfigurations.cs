using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ExternalPatientId).HasMaxLength(100);
        builder.Property(p => p.AssignedDoctorId).HasMaxLength(450).IsRequired();
        builder.Property(p => p.Summary).HasColumnType("text");
        builder.HasIndex(p => new { p.AssignedDoctorId, p.ExternalPatientId })
            .IsUnique()
            .HasFilter("\"ExternalPatientId\" IS NOT NULL");
    }
}

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
        builder.HasIndex(c => new { c.DoctorId, c.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasOne<Patient>().WithMany().HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

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
        builder.HasIndex(t => t.ConsultationId).IsUnique();
        builder.HasIndex(t => t.ExternalJobId);
    }
}

public class MedicalStructuredDataConfiguration : IEntityTypeConfiguration<MedicalStructuredData>
{
    public void Configure(EntityTypeBuilder<MedicalStructuredData> builder)
    {
        builder.ToTable("MedicalStructuredData");
        builder.Property(m => m.SchemaVersion).HasMaxLength(20).IsRequired();
        builder.Property(m => m.StructuredPayload).HasColumnType("jsonb").IsRequired();
    }
}

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

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.Property(a => a.Action).HasMaxLength(200).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.Details).HasMaxLength(2000);
    }
}

public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("ErrorLogs");
        builder.Property(e => e.Message).HasMaxLength(4000);
        builder.Property(e => e.Path).HasMaxLength(500);
        builder.Property(e => e.Method).HasMaxLength(20);
    }
}

public class DoctorNoteConfiguration : IEntityTypeConfiguration<DoctorNote>
{
    public void Configure(EntityTypeBuilder<DoctorNote> builder)
    {
        builder.ToTable("DoctorNotes");
        builder.Property(n => n.DoctorId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.Content).HasColumnType("text").IsRequired();

        builder.HasIndex(n => new { n.DoctorId, n.PatientId, n.ConsultationId });
        builder.HasOne<Patient>().WithMany().HasForeignKey(n => n.PatientId).OnDelete(DeleteBehavior.Restrict);

        // ConsultationId is optional (note may be written without being tied to a specific session).
        builder.HasOne<Consultation>().WithMany().HasForeignKey(n => n.ConsultationId).OnDelete(DeleteBehavior.Restrict);
    }
}

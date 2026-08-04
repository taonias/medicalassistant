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
        builder.HasIndex(m => new { m.ConsumerName, m.EventId }).IsUnique();
        builder.HasIndex(m => new { m.ConsumerName, m.Status, m.LeaseExpiresAtUtc });
    }
}

public class ConsultationDeletionCleanupConfiguration : IEntityTypeConfiguration<ConsultationDeletionCleanup>
{
    public void Configure(EntityTypeBuilder<ConsultationDeletionCleanup> builder)
    {
        builder.ToTable("ConsultationDeletionCleanups");
        builder.Property(c => c.LastFailureCategory).HasMaxLength(100);
        builder.Property(c => c.LastFailureCode).HasMaxLength(200);
        builder.HasIndex(c => c.ConsultationId).IsUnique();
        builder.HasIndex(c => c.DeletionEventId).IsUnique();
        builder.HasOne<Consultation>().WithMany().HasForeignKey(c => c.ConsultationId).OnDelete(DeleteBehavior.Restrict);
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

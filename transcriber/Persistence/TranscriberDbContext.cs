using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Transcriber.Persistence;

public sealed class TranscriberDbContext : DbContext
{
    public TranscriberDbContext(DbContextOptions<TranscriberDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Transcript> Transcripts => Set<Transcript>();
    public DbSet<Consultation> Consultations => Set<Consultation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Action).HasMaxLength(200).IsRequired();
            builder.Property(a => a.EntityType).HasMaxLength(100);
            builder.Property(a => a.EntityId).HasMaxLength(100);
            builder.Property(a => a.Details).HasMaxLength(2000);
            builder.Property(a => a.UserId).HasMaxLength(450);
            builder.Property(a => a.UserName).HasMaxLength(256);
            builder.Property(a => a.IpAddress).HasMaxLength(64);
        });

        modelBuilder.Entity<Transcript>(builder =>
        {
            builder.ToTable("Transcripts");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.TranscriptText)
                .HasColumnName("Transcript")
                .HasColumnType("text");
            builder.Property(t => t.ExternalJobId).HasMaxLength(128);
            builder.Property(t => t.FailureReason).HasMaxLength(2000);
            builder.HasIndex(t => t.ConsultationId).IsUnique();
            builder.HasIndex(t => t.ExternalJobId);
        });

        modelBuilder.Entity<Consultation>(builder =>
        {
            builder.ToTable("Consultations");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.DoctorId).IsRequired();
            builder.Property(c => c.AudioBlobUri).HasMaxLength(2048);
            builder.Property(c => c.DocumentBlobUri).HasMaxLength(2048);
            builder.Property(c => c.DocumentFileName).HasMaxLength(512);
            builder.Property(c => c.FailureReason).HasMaxLength(2000);
        });
    }
}

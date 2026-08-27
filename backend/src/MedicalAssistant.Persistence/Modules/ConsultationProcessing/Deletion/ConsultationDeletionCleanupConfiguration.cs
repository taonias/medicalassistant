using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Modules.ConsultationProcessing.Deletion;

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

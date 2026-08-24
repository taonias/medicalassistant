using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

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

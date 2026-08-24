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

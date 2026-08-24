using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

public class MedicalStructuredDataConfiguration : IEntityTypeConfiguration<MedicalStructuredData>
{
    public void Configure(EntityTypeBuilder<MedicalStructuredData> builder)
    {
        builder.ToTable("MedicalStructuredData");
        builder.Property(m => m.SchemaVersion).HasMaxLength(20).IsRequired();
        builder.Property(m => m.StructuredPayload).HasColumnType("jsonb").IsRequired();
    }
}

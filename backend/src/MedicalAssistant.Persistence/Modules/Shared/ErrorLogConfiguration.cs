using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

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

using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Modules.Assistance.Chat;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.Property(c => c.DoctorId).HasMaxLength(450).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.RollingSummary).HasColumnType("text");

        builder.HasIndex(c => new { c.DoctorId, c.PatientId });

        builder.HasOne<Patient>().WithMany().HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Consultation>().WithMany().HasForeignKey(c => c.ConsultationId).OnDelete(DeleteBehavior.Restrict);

        // The conversation owns its messages: deleting it removes the whole thread.
        builder.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

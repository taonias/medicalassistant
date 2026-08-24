using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

public class MessageCitationConfiguration : IEntityTypeConfiguration<MessageCitation>
{
    public void Configure(EntityTypeBuilder<MessageCitation> builder)
    {
        builder.ToTable("MessageCitations");
        builder.Property(c => c.Label).HasMaxLength(16).IsRequired();
        builder.Property(c => c.DocumentId).HasMaxLength(512).IsRequired();
        builder.Property(c => c.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(c => c.SessionId).HasMaxLength(512);
        // Type-specific provenance is arbitrary JSON text; stored as text (not jsonb) so an
        // unexpected provenance shape can never fail the insert.
        builder.Property(c => c.SourceRef).HasColumnType("text");
        builder.Property(c => c.Quote).HasColumnType("text").IsRequired();

        builder.HasIndex(c => c.ChatMessageId);
    }
}

using MedicalAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAssistant.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.Property(m => m.Content).HasColumnType("text").IsRequired();
        builder.Property(m => m.FailureReason).HasMaxLength(2000);
        builder.Property(m => m.Language).HasMaxLength(20);

        // One message per (conversation, sequence); AskId powers idempotency and retry lookups.
        builder.HasIndex(m => new { m.ConversationId, m.Sequence }).IsUnique();
        builder.HasIndex(m => m.AskId);

        builder.HasMany(m => m.Citations).WithOne().HasForeignKey(c => c.ChatMessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Infrastructure.Persistence.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.Type)
            .HasColumnName("type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(m => m.OccurredOn)
            .HasColumnName("occurred_on")
            .IsRequired();

        builder.Property(m => m.ProcessedOn)
            .HasColumnName("processed_on")
            .IsRequired(false);

        builder.Property(m => m.Error)
            .HasColumnName("error")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(m => m.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(m => m.TraceParent)
            .HasColumnName("trace_parent")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(m => m.TraceState)
            .HasColumnName("trace_state")
            .HasMaxLength(500)
            .IsRequired(false);

        // Filtered index for pending messages
        builder.HasIndex(m => new { m.ProcessedOn, m.OccurredOn })
            .HasDatabaseName("ix_outbox_messages_processed_occurred");
    }
}

public sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");

        builder.HasKey(m => m.MessageId);

        builder.Property(m => m.MessageId)
            .HasColumnName("message_id");

        builder.Property(m => m.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.ProcessedAt)
            .HasColumnName("processed_at")
            .IsRequired();
    }
}

using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RunId)
            .HasColumnName("run_id");

        builder.Property(x => x.RelatedEntityType)
            .HasColumnName("related_entity_type")
            .HasMaxLength(50);

        builder.Property(x => x.RelatedEntityId)
            .HasColumnName("related_entity_id");

        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DetailPayload)
            .HasColumnName("detail_payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne<ReconciliationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CHK_Audit_RelatedEntityPair",
                "(\"related_entity_type\" IS NULL AND \"related_entity_id\" IS NULL) " +
                "OR " +
                "(\"related_entity_type\" IS NOT NULL AND \"related_entity_id\" IS NOT NULL)");

            table.HasCheckConstraint(
                "CHK_Audit_EventType",
                "\"event_type\" IN (" +
                "'BatchCreated', " +
                "'BatchValidated', " +
                "'ReconciliationStarted', " +
                "'ReconciliationCompleted', " +
                "'ReconciliationFailed', " +
                "'ReconciliationDecisionRecorded', " +
                "'ExceptionCreated', " +
                "'AiQuestionAsked', " +
                "'AiToolInvoked', " +
                "'AiExplanationRequested', " +
                "'AiExplanationFailed', " +
                "'AiAssistantFailed')");
        });
    }
}
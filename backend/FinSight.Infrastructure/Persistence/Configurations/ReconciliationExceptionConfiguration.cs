using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class ReconciliationExceptionConfiguration
    : IEntityTypeConfiguration<ReconciliationException>
{
    public void Configure(
        EntityTypeBuilder<ReconciliationException> builder)
    {
        builder.ToTable("reconciliation_exceptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RunId)
            .HasColumnName("run_id")
            .IsRequired();

        builder.Property(x => x.ReconciliationResultId)
            .HasColumnName("reconciliation_result_id")
            .IsRequired();

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.InvolvedSources)
            .HasColumnName("involved_sources")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.DiscrepancyDetail)
            .HasColumnName("discrepancy_detail")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.AiExplanation)
            .HasColumnName("ai_explanation");

        builder.Property(x => x.AiSuggestedCategory)
            .HasColumnName("ai_suggested_category")
            .HasMaxLength(100);

        builder.Property(x => x.AiExplanationGeneratedAt)
            .HasColumnName("ai_explanation_generated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.ReconciliationResultId)
            .IsUnique()
            .HasDatabaseName("UQ_Exception_Result");

        builder.HasIndex(x => new
        {
            x.RunId,
            x.CreatedAt
        })
        .HasDatabaseName("IX_Exception_Run_CreatedAt");

        builder.HasOne<ReconciliationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ReconciliationResult>()
            .WithOne()
            .HasForeignKey<ReconciliationException>(
                x => x.ReconciliationResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CHK_Exception_Category",
                "\"category\" IN ('AmountMismatch', 'DateMismatch', 'MissingRecord', 'DuplicateRecord', 'Unresolved')");
        });
    }
}
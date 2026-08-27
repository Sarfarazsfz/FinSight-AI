using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class ReconciliationResultConfiguration
    : IEntityTypeConfiguration<ReconciliationResult>
{
    public void Configure(
        EntityTypeBuilder<ReconciliationResult> builder)
    {
        builder.ToTable("reconciliation_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RunId)
            .HasColumnName("run_id")
            .IsRequired();

        builder.Property(x => x.NormalizedTransactionId)
            .HasColumnName("normalized_transaction_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.StrategyUsed)
            .HasColumnName("strategy_used")
            .HasMaxLength(100);

        builder.Property(x => x.ReasonCode)
            .HasColumnName("reason_code")
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => x.NormalizedTransactionId)
            .IsUnique()
            .HasDatabaseName("UQ_Result_Tx");

        builder.HasIndex(x => new
        {
            x.RunId,
            x.CreatedAt
        })
        .HasDatabaseName("IX_Result_Run_CreatedAt");

        builder.HasOne<ReconciliationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<NormalizedTransaction>()
            .WithOne()
            .HasForeignKey<ReconciliationResult>(
                x => x.NormalizedTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CHK_Result_Status",
                "\"status\" IN ('Matched', 'Mismatched', 'Missing', 'Duplicate', 'Unresolved')");
        });
    }
}
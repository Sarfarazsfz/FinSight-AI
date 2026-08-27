using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class ReconciliationRunConfiguration
    : IEntityTypeConfiguration<ReconciliationRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationRun> builder)
    {
        builder.ToTable("reconciliation_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.TotalReconciliationUnits)
            .HasColumnName("total_reconciliation_units")
            .IsRequired();

        builder.Property(x => x.MatchRate)
            .HasColumnName("match_rate")
            .HasPrecision(5, 2);

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne<Batch>()
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CHK_Run_Status",
                "\"status\" IN ('Pending', 'Running', 'Completed', 'Failed')");

            table.HasCheckConstraint(
                "CHK_Run_TotalUnits",
                "\"total_reconciliation_units\" >= 0");

            table.HasCheckConstraint(
                "CHK_Run_MatchRate",
                "\"match_rate\" IS NULL OR (\"match_rate\" >= 0 AND \"match_rate\" <= 100)");
        });
    }
}
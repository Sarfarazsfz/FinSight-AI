using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("batches");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.BatchLabel)
            .HasColumnName("batch_label")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PaymentRecordCount)
            .HasColumnName("payment_record_count")
            .IsRequired();

        builder.Property(x => x.BankRecordCount)
            .HasColumnName("bank_record_count")
            .IsRequired();

        builder.Property(x => x.SettlementRecordCount)
            .HasColumnName("settlement_record_count")
            .IsRequired();

        builder.Property(x => x.TotalRecordCount)
            .HasColumnName("total_record_count")
            .IsRequired();

        builder.Property(x => x.ValidationStatus)
            .HasColumnName("validation_status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CHK_Batch_PaymentRecordCount",
                "\"payment_record_count\" >= 0");

            table.HasCheckConstraint(
                "CHK_Batch_BankRecordCount",
                "\"bank_record_count\" >= 0");

            table.HasCheckConstraint(
                "CHK_Batch_SettlementRecordCount",
                "\"settlement_record_count\" >= 0");

            table.HasCheckConstraint(
                "CHK_Batch_TotalRecordCount",
                "\"total_record_count\" >= 0");

            table.HasCheckConstraint(
                "CHK_Batch_ValidationStatus",
                "\"validation_status\" IN ('Valid', 'Invalid')");
        });
    }
}

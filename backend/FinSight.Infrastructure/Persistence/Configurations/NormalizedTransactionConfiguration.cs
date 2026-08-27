using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class NormalizedTransactionConfiguration
    : IEntityTypeConfiguration<NormalizedTransaction>
{
    public void Configure(
        EntityTypeBuilder<NormalizedTransaction> builder)
    {
        builder.ToTable("normalized_transactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RunId)
            .HasColumnName("run_id")
            .IsRequired();

        builder.Property(x => x.TransactionReference)
            .HasColumnName("transaction_reference")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PaymentRecordId)
            .HasColumnName("payment_record_id");

        builder.Property(x => x.BankRecordId)
            .HasColumnName("bank_record_id");

        builder.Property(x => x.SettlementRecordId)
            .HasColumnName("settlement_record_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // One logical transaction reference per reconciliation run.
        builder.HasIndex(x => new
        {
            x.RunId,
            x.TransactionReference
        })
        .IsUnique()
        .HasDatabaseName("UQ_NormalizedTx_Ref");

        // Run -> NormalizedTransaction
        builder.HasOne<ReconciliationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional link -> PaymentRecord.
        builder.HasOne<PaymentRecord>()
            .WithMany()
            .HasForeignKey(x => x.PaymentRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        // Optional link -> BankRecord.
        builder.HasOne<BankRecord>()
            .WithMany()
            .HasForeignKey(x => x.BankRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        // Optional link -> SettlementRecord.
        builder.HasOne<SettlementRecord>()
            .WithMany()
            .HasForeignKey(x => x.SettlementRecordId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
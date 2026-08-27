using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class PaymentRecordConfiguration
    : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(
        EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("payment_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();

        builder.Property(x => x.SourceRecordIdentifier)
            .HasColumnName("source_record_identifier")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TransactionReference)
            .HasColumnName("transaction_reference")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.TransactionDate)
            .HasColumnName("transaction_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.BatchId,
            x.SourceRecordIdentifier
        })
        .IsUnique()
        .HasDatabaseName("UQ_Payment_TechDup");

        builder.HasIndex(x => new
        {
            x.BatchId,
            x.TransactionReference
        })
        .HasDatabaseName(
            "IX_Payment_Batch_TransactionReference");

        builder.HasOne<Batch>()
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<BankRecord> BankRecords => Set<BankRecord>();
    public DbSet<SettlementRecord> SettlementRecords => Set<SettlementRecord>();
    public DbSet<NormalizedTransaction> NormalizedTransactions => Set<NormalizedTransaction>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();
    public DbSet<ReconciliationResult> ReconciliationResults => Set<ReconciliationResult>();
    public DbSet<ReconciliationException> ReconciliationExceptions => Set<ReconciliationException>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}

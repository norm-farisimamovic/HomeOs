using HomeOs.Modules.Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Finance.Persistence;

/// <summary>EF Core context for the Finance module.</summary>
public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("Transactions");
            e.HasKey(t => t.Id);
            e.Property(t => t.Amount).HasPrecision(12, 2);
            e.Property(t => t.Currency).HasMaxLength(8).IsRequired();
            e.Property(t => t.Category).HasMaxLength(60).IsRequired();
            e.Property(t => t.Description).HasMaxLength(400);
            e.Property(t => t.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Visibility).HasConversion<string>().HasMaxLength(16);
            e.Ignore(t => t.ObjectType);
            e.HasIndex(t => t.HouseholdId);
            e.HasIndex(t => t.OccurredOn);
        });

        modelBuilder.Entity<Bill>(e =>
        {
            e.ToTable("Bills");
            e.HasKey(b => b.Id);
            e.Property(b => b.Name).HasMaxLength(120).IsRequired();
            e.Property(b => b.Amount).HasPrecision(12, 2);
            e.Property(b => b.Currency).HasMaxLength(8).IsRequired();
            e.Property(b => b.Category).HasMaxLength(60).IsRequired();
            e.Property(b => b.Cadence).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.Visibility).HasConversion<string>().HasMaxLength(16);
            e.Ignore(b => b.ObjectType);
            e.HasIndex(b => b.HouseholdId);
            e.HasIndex(b => b.NextDue);
        });

        modelBuilder.Entity<Budget>(e =>
        {
            e.ToTable("Budgets");
            e.HasKey(b => b.Id);
            e.Property(b => b.Category).HasMaxLength(60).IsRequired();
            e.Property(b => b.MonthlyLimit).HasPrecision(12, 2);
            e.Property(b => b.Currency).HasMaxLength(8).IsRequired();
            e.HasIndex(b => new { b.HouseholdId, b.Category }).IsUnique();
        });
    }
}

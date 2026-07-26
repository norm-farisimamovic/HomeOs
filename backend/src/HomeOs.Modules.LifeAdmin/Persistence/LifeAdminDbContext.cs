using HomeOs.Modules.LifeAdmin.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.LifeAdmin.Persistence;

/// <summary>EF Core context owning the Life-admin module's table.</summary>
public sealed class LifeAdminDbContext(DbContextOptions<LifeAdminDbContext> options) : DbContext(options)
{
    public DbSet<LifeRecord> Records => Set<LifeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var r = modelBuilder.Entity<LifeRecord>();
        r.ToTable("LifeRecords");
        r.HasKey(x => x.Id);
        r.Property(x => x.Title).HasMaxLength(200).IsRequired();
        r.Property(x => x.Provider).HasMaxLength(200);
        r.Property(x => x.Notes).HasMaxLength(2000);
        r.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        r.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(16);
        r.Ignore(x => x.ObjectType);
        r.HasIndex(x => new { x.HouseholdId, x.ExpiresOn });
    }
}

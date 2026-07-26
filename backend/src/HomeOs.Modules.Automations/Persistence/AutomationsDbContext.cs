using HomeOs.Modules.Automations.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Automations.Persistence;

/// <summary>EF Core context owning the Automations module's table.</summary>
public sealed class AutomationsDbContext(DbContextOptions<AutomationsDbContext> options) : DbContext(options)
{
    public DbSet<Automation> Automations => Set<Automation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var a = modelBuilder.Entity<Automation>();
        a.ToTable("Automations");
        a.HasKey(x => x.Id);
        a.Property(x => x.Name).HasMaxLength(120).IsRequired();
        a.Property(x => x.Trigger).HasMaxLength(60).IsRequired();
        a.Property(x => x.Action).HasMaxLength(60).IsRequired();
        a.Property(x => x.Message).HasMaxLength(400);
        a.HasIndex(x => new { x.HouseholdId, x.Trigger, x.Enabled });
    }
}

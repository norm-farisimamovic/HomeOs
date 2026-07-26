using HomeOs.Modules.Reminders.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Reminders.Persistence;

/// <summary>EF Core context owning the Reminders module's table.</summary>
public sealed class RemindersDbContext(DbContextOptions<RemindersDbContext> options) : DbContext(options)
{
    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var r = modelBuilder.Entity<Reminder>();
        r.ToTable("Reminders");
        r.HasKey(x => x.Id);
        r.Property(x => x.Title).HasMaxLength(200).IsRequired();
        r.Property(x => x.Notes).HasMaxLength(2000);
        r.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(16);
        r.Property(x => x.SourceKey).HasMaxLength(40);
        r.Ignore(x => x.ObjectType);
        r.HasIndex(x => new { x.HouseholdId, x.RemindOn });
        r.HasIndex(x => new { x.HouseholdId, x.SourceKey, x.SourceId });
    }
}

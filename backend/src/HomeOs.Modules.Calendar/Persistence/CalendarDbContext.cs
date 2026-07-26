using HomeOs.Modules.Calendar.Domain;
using HomeOs.Platform.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Calendar.Persistence;

/// <summary>EF Core context owning the Calendar module's tables (PascalCase, isolated per module).</summary>
public sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<CalendarEvent>();
        e.ToTable("CalendarEvents");
        e.HasKey(x => x.Id);
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.Location).HasMaxLength(200);
        e.Property(x => x.Notes).HasMaxLength(2000);
        e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(16);
        e.Ignore(x => x.ObjectType);
        e.HasIndex(x => new { x.HouseholdId, x.StartsOn });
    }
}

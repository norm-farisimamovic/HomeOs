using HomeOs.Modules.Notes.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Notes.Persistence;

/// <summary>EF Core context owning the Notes module's table.</summary>
public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var n = modelBuilder.Entity<Note>();
        n.ToTable("Notes");
        n.HasKey(x => x.Id);
        n.Property(x => x.Title).HasMaxLength(200).IsRequired();
        n.Property(x => x.Content).HasMaxLength(8000);
        n.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(16);
        n.Ignore(x => x.ObjectType);
        n.HasIndex(x => new { x.HouseholdId, x.Pinned, x.UpdatedAtUtc });
    }
}

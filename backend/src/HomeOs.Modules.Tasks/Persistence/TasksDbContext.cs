using System.Text.Json;
using HomeOs.Modules.Tasks.Domain;
using HomeOs.Platform.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HomeOs.Modules.Tasks.Persistence;

/// <summary>EF Core context for the Tasks module. Owns only its own table; links across apps go via EntityLink.</summary>
public sealed class TasksDbContext(DbContextOptions<TasksDbContext> options) : DbContext(options)
{
    /// <summary>Tasks.</summary>
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    /// <summary>Kanban boards.</summary>
    public DbSet<Board> Boards => Set<Board>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tagsComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("Tasks");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(200).IsRequired();
            e.Property(t => t.Description).HasMaxLength(2000);
            e.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Visibility).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .HasColumnType("json")
                .Metadata.SetValueComparer(tagsComparer);
            e.Ignore(t => t.ObjectType);
            e.Ignore(t => t.IsDone);
            e.HasIndex(t => t.HouseholdId);
            e.HasIndex(t => t.DueDate);
            e.HasIndex(t => t.AssigneeId);
        });

        modelBuilder.Entity<Board>(e =>
        {
            e.ToTable("Boards");
            e.HasKey(b => b.Id);
            e.Property(b => b.Name).HasMaxLength(80).IsRequired();
            e.Property(b => b.Color).HasMaxLength(40).IsRequired();
            e.HasIndex(b => b.HouseholdId);
        });
    }
}

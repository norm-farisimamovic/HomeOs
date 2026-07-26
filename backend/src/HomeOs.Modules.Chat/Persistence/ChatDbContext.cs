using HomeOs.Modules.Chat.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Chat.Persistence;

/// <summary>EF Core context owning the Chat module's table.</summary>
public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.ToTable("ChatMessages");
            e.HasKey(m => m.Id);
            e.Property(m => m.Text).HasMaxLength(2000).IsRequired();
            e.HasIndex(m => new { m.HouseholdId, m.SentAtUtc });
        });
    }
}

using HomeOs.Modules.Shopping.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Shopping.Persistence;

/// <summary>EF Core context owning the Shopping module's tables.</summary>
public sealed class ShoppingDbContext(DbContextOptions<ShoppingDbContext> options) : DbContext(options)
{
    public DbSet<ShoppingList> Lists => Set<ShoppingList>();
    public DbSet<ShoppingItem> Items => Set<ShoppingItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShoppingList>(e =>
        {
            e.ToTable("ShoppingLists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.ListId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HouseholdId);
        });

        modelBuilder.Entity<ShoppingItem>(e =>
        {
            e.ToTable("ShoppingItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.ListId);
        });
    }
}

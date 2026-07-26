using HomeOs.Platform.Apps;
using HomeOs.Platform.Attachments;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Links;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Persistence;

/// <summary>
/// EF Core context for the platform kernel — ASP.NET Core Identity (members &amp; roles) plus the
/// kernel's own tables (households; later: entity links, notifications). App modules use their own
/// <see cref="DbContext"/> against the same database and communicate only through platform contracts.
/// </summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : IdentityDbContext<Member, IdentityRole<Guid>, Guid>(options)
{
    /// <summary>Households (the tenancy root).</summary>
    public DbSet<Household> Households => Set<Household>();

    /// <summary>Pending household invitations.</summary>
    public DbSet<HouseholdInvite> HouseholdInvites => Set<HouseholdInvite>();

    /// <summary>In-app notifications (the bell feed).</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>Per-member email preferences for notification categories.</summary>
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    /// <summary>Household audit log (owner/admin visibility).</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Per-household app state (enabled + granted capabilities).</summary>
    public DbSet<HouseholdApp> HouseholdApps => Set<HouseholdApp>();

    /// <summary>Cross-app object links (the "connected web").</summary>
    public DbSet<EntityLink> EntityLinks => Set<EntityLink>();

    /// <summary>Member profile pictures.</summary>
    public DbSet<MemberAvatar> MemberAvatars => Set<MemberAvatar>();

    /// <summary>Files attached to app entities (tasks, bills, documents…).</summary>
    public DbSet<Attachment> Attachments => Set<Attachment>();

    /// <summary>Gamification points ledger (household scoreboard).</summary>
    public DbSet<Scoreboard.PointsEntry> PointsEntries => Set<Scoreboard.PointsEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Household>(entity =>
        {
            entity.ToTable("Households");
            entity.Property(h => h.Name).HasMaxLength(120).IsRequired();
            entity.HasMany(h => h.Members)
                  .WithOne(m => m.Household!)
                  .HasForeignKey(m => m.HouseholdId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.Property(m => m.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(m => m.PreferredCulture).HasMaxLength(10).IsRequired();
            entity.Property(m => m.PreferredCurrency).HasMaxLength(10).IsRequired();
            entity.Property(m => m.DigestFrequency).HasConversion<int>();
            entity.HasIndex(m => m.HouseholdId);
        });

        modelBuilder.Entity<HouseholdInvite>(entity =>
        {
            entity.ToTable("HouseholdInvites");
            entity.Property(i => i.Email).HasMaxLength(256).IsRequired();
            entity.Property(i => i.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(i => i.Role).HasMaxLength(32).IsRequired();
            entity.Property(i => i.Token).HasMaxLength(64).IsRequired();
            entity.HasIndex(i => i.Token).IsUnique();
            entity.HasIndex(i => i.HouseholdId);
            entity.Ignore(i => i.IsPending);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.Property(n => n.Category).HasMaxLength(40).IsRequired();
            entity.Property(n => n.Title).HasMaxLength(200).IsRequired();
            entity.Property(n => n.Body).HasMaxLength(1000);
            entity.Property(n => n.Link).HasMaxLength(200);
            entity.HasIndex(n => new { n.MemberId, n.IsRead, n.CreatedAtUtc });
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("NotificationPreferences");
            entity.Property(p => p.Category).HasMaxLength(40).IsRequired();
            entity.HasIndex(p => new { p.MemberId, p.Category }).IsUnique();
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("AuditEntries");
            entity.Property(a => a.Action).HasMaxLength(60).IsRequired();
            entity.Property(a => a.Detail).HasMaxLength(1000);
            entity.HasIndex(a => new { a.HouseholdId, a.CreatedAtUtc });
        });

        modelBuilder.Entity<HouseholdApp>(entity =>
        {
            entity.ToTable("HouseholdApps");
            entity.Property(a => a.AppId).HasMaxLength(40).IsRequired();
            entity.Property(a => a.GrantedCapabilities).IsRequired();
            entity.HasIndex(a => new { a.HouseholdId, a.AppId }).IsUnique();
        });

        modelBuilder.Entity<MemberAvatar>(entity =>
        {
            entity.ToTable("MemberAvatars");
            entity.HasKey(a => a.MemberId);
            entity.Property(a => a.ContentType).HasMaxLength(80).IsRequired();
            entity.Property(a => a.Data).HasColumnType("longblob").IsRequired();
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("Attachments");
            entity.Property(a => a.OwnerType).HasMaxLength(40).IsRequired();
            entity.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            entity.Property(a => a.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(a => a.Data).HasColumnType("longblob").IsRequired();
            entity.HasIndex(a => new { a.HouseholdId, a.OwnerType, a.OwnerId });
        });

        modelBuilder.Entity<Scoreboard.PointsEntry>(entity =>
        {
            entity.ToTable("PointsEntries");
            entity.Property(p => p.SourceKey).HasMaxLength(40).IsRequired();
            entity.HasIndex(p => new { p.HouseholdId, p.SourceKey, p.SourceId }).IsUnique();
            entity.HasIndex(p => new { p.HouseholdId, p.MemberId });
        });

        modelBuilder.Entity<EntityLink>(entity =>
        {
            entity.ToTable("EntityLinks");
            entity.Property(l => l.FromType).HasMaxLength(40).IsRequired();
            entity.Property(l => l.ToType).HasMaxLength(40).IsRequired();
            entity.Property(l => l.ToTitle).HasMaxLength(200).IsRequired();
            entity.Property(l => l.ToLink).HasMaxLength(200).IsRequired();
            entity.HasIndex(l => new { l.HouseholdId, l.FromType, l.FromId });
        });

        // Pick up IEntityTypeConfiguration<T> implementations as more kernel entities are added.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}

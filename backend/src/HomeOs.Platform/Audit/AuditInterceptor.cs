using System.Runtime.CompilerServices;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Platform.Audit;

/// <summary>
/// Records <b>every</b> create/update/delete on a module's data to the household audit log, attributed to the
/// acting member — so "who changed what, when" is captured with no per-endpoint wiring. Added to each module's
/// <see cref="DbContext"/>. Only fires for authenticated requests (background jobs have no actor and are
/// skipped). Never audits the platform's own tables (it writes there) to avoid re-entrancy.
/// </summary>
public sealed class AuditInterceptor(IHttpContextAccessor accessor) : SaveChangesInterceptor
{
    private sealed record Pending(string Action, string Detail);

    // Per-DbContext-instance list of changes captured before save, written after it succeeds.
    private static readonly ConditionalWeakTable<DbContext, List<Pending>> Captured = new();

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } ctx) Capture(ctx);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } ctx) Capture(ctx);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await WriteAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        WriteAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    private void Capture(DbContext ctx)
    {
        // Only capture when there's an authenticated member to attribute the change to.
        var services = accessor.HttpContext?.RequestServices;
        if (services?.GetService<ICurrentMember>() is not { IsAuthenticated: true }) return;

        var list = new List<Pending>();
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            var action = entry.State switch
            {
                EntityState.Added => "created",
                EntityState.Modified => "updated",
                EntityState.Deleted => "deleted",
                _ => null,
            };
            if (action is null) continue;

            var type = entry.Entity.GetType().Name;
            var label = ReadLabel(entry) ?? type;
            var detail = $"{type}: {label}";
            if (entry.State == EntityState.Modified)
            {
                var changes = DescribeChanges(entry);
                if (changes.Length > 0) detail += $" — {changes}";
            }
            list.Add(new Pending($"{Slug(type)}.{action}", Trim(detail, 1000)));
        }
        if (list.Count > 0) Captured.AddOrUpdate(ctx, list);
    }

    // "Priority: Normal → High; DueDate: 2026-07-24 → 2026-07-31" for the meaningful changed fields.
    private static string DescribeChanges(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var parts = new List<string>();
        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified || prop.Metadata.IsPrimaryKey()) continue;
            var name = prop.Metadata.Name;
            // Skip bookkeeping/noisy columns.
            if (name.EndsWith("AtUtc", StringComparison.Ordinal) || name is "NotifiedLeadDays" or "GrantedCapabilities") continue;
            var before = Show(prop.OriginalValue);
            var after = Show(prop.CurrentValue);
            if (before == after) continue;
            parts.Add($"{name}: {before} → {after}");
            if (parts.Count >= 6) break;
        }
        return string.Join("; ", parts);
    }

    private static string Show(object? value)
    {
        if (value is null) return "∅";
        var s = value.ToString() ?? "";
        if (s.Length > 60) s = s[..60] + "…";
        return s.Length == 0 ? "∅" : s;
    }

    private async Task WriteAsync(DbContext? ctx, CancellationToken ct)
    {
        if (ctx is null || !Captured.TryGetValue(ctx, out var list)) return;
        Captured.Remove(ctx);

        var services = accessor.HttpContext?.RequestServices;
        if (services is null) return;

        var audit = services.GetService<IAuditLog>();
        if (audit is not null)
            foreach (var p in list)
                await audit.RecordAsync(p.Action, p.Detail, ct);

        // Live-refresh everyone in the household: their open screens refetch (dashboard, lists, board…).
        var me = services.GetService<ICurrentMember>();
        var hub = services.GetService<IHubContext<NotificationsHub>>();
        if (me is { IsAuthenticated: true } && hub is not null)
        {
            var sources = list.Select(p => p.Action.Split('.')[0]).Distinct().ToArray();
            await hub.Clients.Group(NotificationsHub.HouseholdGroup(me.HouseholdId))
                .SendAsync("changed", new { sources }, ct);
        }
    }

    // Best-effort human label: a Title/Name/DisplayName property if the entity has one.
    private static string? ReadLabel(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        foreach (var name in (string[])["Title", "Name", "DisplayName"])
        {
            var prop = entry.Metadata.FindProperty(name);
            if (prop is null) continue;
            var value = entry.Property(name).CurrentValue?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static string Slug(string typeName) =>
        typeName.EndsWith("Item", StringComparison.Ordinal) ? typeName[..^4].ToLowerInvariant() : typeName.ToLowerInvariant();

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}

/// <summary>Adds the audit interceptor to a module's DbContext. Call from the module's <c>AddDbContextPool</c>.</summary>
public static class DbContextAuditingExtensions
{
    /// <summary>Records every create/update/delete on this context to the household audit log.</summary>
    public static DbContextOptionsBuilder AddAuditing(this DbContextOptionsBuilder options, IServiceProvider services) =>
        options.AddInterceptors(services.GetRequiredService<AuditInterceptor>());
}

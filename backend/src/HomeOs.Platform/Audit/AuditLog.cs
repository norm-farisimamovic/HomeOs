using HomeOs.Platform.Events;
using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;

namespace HomeOs.Platform.Audit;

/// <summary>Kernel capability: record an audit entry attributed to the current member.</summary>
public interface IAuditLog
{
    Task RecordAsync(string action, string detail, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AuditLog(PlatformDbContext db, ICurrentMember me) : IAuditLog
{
    /// <inheritdoc />
    public async Task RecordAsync(string action, string detail, CancellationToken cancellationToken = default)
    {
        db.AuditEntries.Add(AuditEntry.Create(me.HouseholdId, me.Id, action, detail));
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Feeds the audit log from the generic <see cref="AppActivity"/> stream — so every "notable moment" any
/// app announces (task completed, bill added, event scheduled…) is recorded with no per-app wiring.
/// </summary>
public sealed class AuditActivityHandler(PlatformDbContext db) : IEventHandler<AppActivity>
{
    /// <inheritdoc />
    public async Task Handle(AppActivity domainEvent, CancellationToken cancellationToken)
    {
        // High-frequency activity (e.g. chat messages) participates in automations + the connected web via
        // the event bus, but would swamp the audit log — so it's announced but not recorded here.
        if (domainEvent.Kind.StartsWith("chat.", StringComparison.Ordinal)) return;
        db.AuditEntries.Add(AuditEntry.Create(domainEvent.HouseholdId, domainEvent.ActorMemberId, domainEvent.Kind, domainEvent.Title));
        await db.SaveChangesAsync(cancellationToken);
    }
}

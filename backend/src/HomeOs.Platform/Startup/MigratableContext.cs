namespace HomeOs.Platform.Startup;

/// <summary>
/// Marks a <c>DbContext</c> type that the startup initializer should create/migrate. The platform
/// registers its own; each module registers its context the same way — no special-casing.
/// </summary>
public sealed record MigratableContext(Type ContextType);

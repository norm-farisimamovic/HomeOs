namespace HomeOs.Platform.Seeding;

/// <summary>
/// Seeds initial data into the database. Each app/module contributes its own seeder
/// (registered with <c>services.AddDataSeeder&lt;T&gt;()</c>); the platform runs them all,
/// in <see cref="Order"/>, on startup after migrations.
/// </summary>
/// <remarks>
/// Seeders MUST be idempotent — they run on every startup, so check for existence before
/// inserting (e.g. seed only if the table is empty / a given key is missing).
/// See <c>docs/SEEDING.md</c>.
/// </remarks>
public interface IDataSeeder
{
    /// <summary>Relative run order (ascending). Lower runs first; use gaps (10, 20, …) to allow inserts.</summary>
    int Order { get; }

    /// <summary>Idempotently inserts this seeder's initial data.</summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}

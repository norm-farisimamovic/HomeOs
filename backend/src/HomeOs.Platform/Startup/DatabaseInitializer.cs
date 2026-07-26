using HomeOs.Platform.Persistence;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeOs.Platform.Startup;

/// <summary>Startup database bootstrap: create/migrate the schema, then run data seeders.</summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// On startup: applies EF Core migrations for the platform (creating the database if it does
    /// not exist), then runs all registered <see cref="IDataSeeder"/>s in order. Both steps are
    /// gated by config (<c>Database:AutoMigrate</c>, <c>Database:Seed</c>, both default <c>true</c>).
    /// Failures are logged and the app still starts (so <c>/health/ready</c> surfaces the problem),
    /// rather than crash-looping.
    /// </summary>
    public static async Task InitializeHomeOsDatabaseAsync(this IHost app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("HomeOs.Database");

        if (config.GetValue("Database:AutoMigrate", true))
        {
            // Fresh containers: MySQL may still be initializing even after its healthcheck passes, so the
            // first connection can fail transiently. Wait for the DB to actually accept connections before
            // migrating, so a first-boot timing race never leaves the schema uncreated.
            var platform = services.GetRequiredService<PlatformDbContext>();
            if (!await WaitForDatabaseAsync(platform, logger, cancellationToken))
            {
                logger.LogError("Database did not become reachable in time; skipping migrations. The app will start, but the database may be unavailable.");
                return;
            }

            // Migrate every registered context (platform + each module) — no special-casing.
            foreach (var target in services.GetServices<MigratableContext>())
            {
                try
                {
                    logger.LogInformation("Applying migrations for {Context}…", target.ContextType.Name);
                    var ctx = (DbContext)services.GetRequiredService(target.ContextType);
                    await ctx.Database.MigrateAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Migration failed for {Context}. The app will start, but the database may be unavailable.", target.ContextType.Name);
                    return; // don't seed against a broken/unreachable schema
                }
            }
            logger.LogInformation("Database is up to date.");
        }

        if (config.GetValue("Database:Seed", true))
        {
            var seeders = services.GetServices<IDataSeeder>().OrderBy(s => s.Order).ToList();
            if (seeders.Count == 0)
            {
                logger.LogInformation("No data seeders registered.");
                return;
            }

            foreach (var seeder in seeders)
            {
                try
                {
                    logger.LogInformation("Seeding: {Seeder}…", seeder.GetType().Name);
                    await seeder.SeedAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Seeder {Seeder} failed.", seeder.GetType().Name);
                }
            }
            logger.LogInformation("Seeding complete.");
        }
    }

    /// <summary>
    /// Polls until the database accepts connections (or a timeout), so migrations don't get skipped when a
    /// freshly-started MySQL container isn't ready yet. ~60s max (30 × 2s) — ample for first-time init.
    /// </summary>
    private static async Task<bool> WaitForDatabaseAsync(DbContext ctx, ILogger logger, CancellationToken ct)
    {
        const int maxAttempts = 30;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (await ctx.Database.CanConnectAsync(ct)) return true;
            }
            catch
            {
                // Server not accepting connections yet — retry.
            }
            if (attempt == 1) logger.LogInformation("Waiting for the database to become available…");
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
        return false;
    }
}

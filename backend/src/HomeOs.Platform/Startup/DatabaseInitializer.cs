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
}

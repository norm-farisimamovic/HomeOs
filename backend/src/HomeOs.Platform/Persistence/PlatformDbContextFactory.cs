using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomeOs.Platform.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the context (for migrations) without booting
/// the API host. Uses <c>HOMEOS_DB</c> if set, else the local dev connection string.
/// Not used at runtime — the app configures the context via <c>AddHomeOsPlatform</c>.
/// </summary>
public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    /// <inheritdoc />
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HOMEOS_DB")
            ?? "Server=localhost;Port=3306;Database=homeos;User Id=homeos;Password=homeos;SslMode=None;AllowPublicKeyRetrieval=True";

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42)))
            .Options;

        return new PlatformDbContext(options);
    }
}

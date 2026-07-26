using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomeOs.Modules.Finance.Persistence;

/// <summary>Design-time factory so <c>dotnet ef</c> can build the context without booting the host.</summary>
public sealed class FinanceDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HOMEOS_DB")
            ?? "Server=localhost;Port=3306;Database=homeos;User Id=homeos;Password=homeos;SslMode=None;AllowPublicKeyRetrieval=True";
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42)))
            .Options;
        return new FinanceDbContext(options);
    }
}

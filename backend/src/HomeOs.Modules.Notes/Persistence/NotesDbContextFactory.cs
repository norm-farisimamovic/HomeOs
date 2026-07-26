using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomeOs.Modules.Notes.Persistence;

/// <summary>Design-time factory so <c>dotnet ef</c> can build the context without booting the host.</summary>
public sealed class NotesDbContextFactory : IDesignTimeDbContextFactory<NotesDbContext>
{
    public NotesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HOMEOS_DB")
            ?? "Server=localhost;Port=3306;Database=homeos;User Id=homeos;Password=homeos;SslMode=None;AllowPublicKeyRetrieval=True";
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42)))
            .Options;
        return new NotesDbContext(options);
    }
}

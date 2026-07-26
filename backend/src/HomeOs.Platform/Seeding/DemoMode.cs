using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Platform.Seeding;

/// <summary>
/// Whether demo data — the "Demo Home" household plus sample content across the apps — should be seeded.
/// On by default in Development; in any other environment only when <c>Demo:Enabled=true</c>, so a production
/// instance can deliberately opt into a showcase dataset without it ever appearing by accident.
/// </summary>
public static class DemoMode
{
    /// <summary>True in Development, or anywhere when <c>Demo:Enabled</c> is set.</summary>
    public static bool IsEnabled(IHostEnvironment env, IConfiguration config) =>
        env.IsDevelopment() || config.GetValue("Demo:Enabled", false);
}

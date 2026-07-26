using System.Reflection;
using Shouldly;

namespace HomeOs.Architecture.Tests;

/// <summary>
/// Guards the modular-monolith rule: no app module may reference another app module. Modules communicate only
/// through the <c>HomeOs.Platform</c> kernel. A direct <c>Modules.X → Modules.Y</c> reference fails the build.
/// </summary>
public class ModuleIsolationTests
{
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(HomeOs.Modules.Tasks.TasksModule).Assembly,
        typeof(HomeOs.Modules.Finance.FinanceModule).Assembly,
        typeof(HomeOs.Modules.Calendar.CalendarModule).Assembly,
        typeof(HomeOs.Modules.Reminders.RemindersModule).Assembly,
        typeof(HomeOs.Modules.Notes.NotesModule).Assembly,
        typeof(HomeOs.Modules.LifeAdmin.LifeAdminModule).Assembly,
        typeof(HomeOs.Modules.Automations.AutomationsModule).Assembly,
        typeof(HomeOs.Modules.Shopping.ShoppingModule).Assembly,
        typeof(HomeOs.Modules.Chat.ChatModule).Assembly,
    ];

    [Fact]
    public void No_module_references_another_module()
    {
        var offences = new List<string>();

        foreach (var module in ModuleAssemblies)
        {
            var self = module.GetName().Name!;
            var badRefs = module.GetReferencedAssemblies()
                .Select(a => a.Name!)
                .Where(name => name.StartsWith("HomeOs.Modules.", StringComparison.Ordinal) && name != self);

            foreach (var bad in badRefs)
                offences.Add($"{self} → {bad}");
        }

        offences.ShouldBeEmpty($"App modules must talk only through the kernel. Illegal references: {string.Join(", ", offences)}");
    }

    [Fact]
    public void Every_module_ships_a_host_module_for_auto_discovery()
    {
        foreach (var module in ModuleAssemblies)
        {
            var hasHostModule = module.GetTypes()
                .Any(t => t is { IsAbstract: false, IsInterface: false }
                          && typeof(HomeOs.Platform.Startup.IHostModule).IsAssignableFrom(t));
            hasHostModule.ShouldBeTrue($"{module.GetName().Name} has no IHostModule — the host can't auto-discover it.");
        }
    }
}

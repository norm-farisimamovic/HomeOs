using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Platform.Startup;

/// <summary>
/// Discovers every <see cref="IHostModule"/> shipped alongside the host (any <c>HomeOs.Modules.*</c> assembly
/// in the app directory) and wires it in. The host calls <see cref="AddHomeOsModules"/> once and
/// <see cref="MapHomeOsModules"/> once — it never names individual modules, so dropping in a new module
/// project (a reference so its DLL ships) is all it takes; nothing else changes.
/// </summary>
public static class ModuleLoader
{
    private static IReadOnlyList<IHostModule>? _cache;

    /// <summary>Finds and instantiates the host modules once (cached), in a stable order.</summary>
    public static IReadOnlyList<IHostModule> Discover()
    {
        if (_cache is not null) return _cache;

        var modules = new List<IHostModule>();
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "HomeOs.Modules.*.dll"))
        {
            var name = TryGetName(path);
            if (name?.Name is null || name.Name.EndsWith(".Tests", StringComparison.Ordinal)) continue;

            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name.Name)
                ?? Assembly.Load(name);

            foreach (var type in assembly.GetTypes()
                         .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IHostModule).IsAssignableFrom(t)))
            {
                if (Activator.CreateInstance(type) is IHostModule module) modules.Add(module);
            }
        }

        _cache = modules.OrderBy(m => m.GetType().Name, StringComparer.Ordinal).ToList();
        return _cache;
    }

    /// <summary>Registers every discovered module's services.</summary>
    public static IServiceCollection AddHomeOsModules(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var module in Discover()) module.Add(services, configuration);
        return services;
    }

    /// <summary>Maps every discovered module's endpoints.</summary>
    public static IEndpointRouteBuilder MapHomeOsModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in Discover()) module.Map(endpoints);
        return endpoints;
    }

    private static AssemblyName? TryGetName(string path)
    {
        try { return AssemblyName.GetAssemblyName(path); }
        catch (BadImageFormatException) { return null; } // not a managed assembly
        catch (FileLoadException) { return null; }
    }
}

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Platform.Startup;

/// <summary>
/// A pluggable app module. Implement this in a module assembly and the host discovers and wires it
/// automatically (see <see cref="ModuleLoader"/>) — adding an app needs no edit to the host or any other
/// module, only a project reference so its DLL ships. This is what makes "new apps are first-class citizens,
/// with no special-casing and no changes to existing apps" concrete on the composition side.
/// </summary>
public interface IHostModule
{
    /// <summary>Registers the module's services (DbContext, endpoints handlers, contracts, background jobs).</summary>
    void Add(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's HTTP endpoints.</summary>
    void Map(IEndpointRouteBuilder endpoints);
}

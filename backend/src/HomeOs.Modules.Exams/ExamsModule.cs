using HomeOs.Modules.Exams.Bank;
using HomeOs.Modules.Exams.Features;
using HomeOs.Modules.Exams.Grading;
using HomeOs.Modules.Exams.Laws;
using HomeOs.Modules.Exams.Persistence;
using HomeOs.Modules.Exams.Search;
using HomeOs.Platform;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Search;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Modules.Exams;

/// <summary>
/// Composition for the Exams (exam-practice) app — a study app for the professional exam covering four laws.
/// Like every Home OS app it plugs in through kernel contracts only: it borrows the configured AI provider via
/// <c>IAssistant</c> to mark written answers and contributes to global search, without referencing any other module.
/// </summary>
public static class ExamsModule
{
    public static IServiceCollection AddExamsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<ExamsDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(ExamsDbContext)));
        // Reference data: the embedded JSON is parsed once for the life of the process.
        services.AddSingleton<QuestionBank>();
        // The four laws' full text, so a question's article citation is readable in place.
        services.AddSingleton<LawLibrary>();
        services.AddScoped<AnswerGrader>();
        services.AddScoped<ISearchProvider, ExamsSearchProvider>();
        services.AddSingleton<IAppModule, ExamsAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapExamsModule(this IEndpointRouteBuilder app) => app.MapExamEndpoints();
}

/// <summary>Exams app manifest.</summary>
public sealed class ExamsAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "exams", "nav.exams", "apps.desc.exams", "GraduationCap", "var(--m-exams)",
        "/exams", "/api/exams", ["read:exams", "write:exams"]);
}

/// <summary>Auto-discovered host wiring for the Exams app.</summary>
public sealed class ExamsHostModule : IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddExamsModule(configuration);

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapExamsModule();
}

using HomeOs.Platform.Money;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HomeOs.Platform.Features.Money;

/// <summary>The supported-currencies registry (for the profile picker).</summary>
public static class CurrenciesEndpoints
{
    public static IEndpointRouteBuilder MapCurrenciesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/currencies", () => Results.Ok(
                Currencies.All.Select(c => new CurrencyDto(c.Code, c.Symbol, c.Name)).ToList()))
            .RequireAuthorization().WithTags("Currencies").WithName("Currencies");
        return app;
    }
}

/// <summary>A currency option for the client.</summary>
public sealed record CurrencyDto(string Code, string Symbol, string Name);

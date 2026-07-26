using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HomeOs.Platform.Features.Weather;

/// <summary>
/// A tiny weather proxy for the dashboard widget. Uses Open-Meteo (free, no API key) and goes through the
/// backend so the browser only ever calls same-origin (keeps the strict frontend CSP intact). Defaults to
/// Sarajevo when no coordinates are given; fails soft (returns null) so the widget just hides.
/// </summary>
public static class WeatherEndpoints
{
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/weather", async (double? lat, double? lon, IHttpClientFactory factory, CancellationToken ct) =>
        {
            var la = lat ?? 43.8563;   // Sarajevo
            var lo = lon ?? 18.4131;
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={la:0.###}&longitude={lo:0.###}" +
                      "&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min&forecast_days=1&timezone=auto";
            try
            {
                var http = factory.CreateClient("weather");
                var json = await http.GetFromJsonAsync<JsonObject>(url, ct);
                var current = json?["current"];
                var daily = json?["daily"];
                if (current is null) return Results.Ok<WeatherDto?>(null);
                return Results.Ok<WeatherDto?>(new WeatherDto(
                    Math.Round(current["temperature_2m"]?.GetValue<double>() ?? 0),
                    current["weather_code"]?.GetValue<int>() ?? 0,
                    Math.Round(daily?["temperature_2m_max"]?[0]?.GetValue<double>() ?? 0),
                    Math.Round(daily?["temperature_2m_min"]?[0]?.GetValue<double>() ?? 0)));
            }
            catch
            {
                return Results.Ok<WeatherDto?>(null);
            }
        }).RequireAuthorization().WithTags("Weather").WithName("Weather");

        return app;
    }
}

/// <summary>Current conditions for the dashboard widget (°C; <c>Code</c> is a WMO weather code the client maps to an icon).</summary>
public sealed record WeatherDto(double TempC, int Code, double HighC, double LowC);

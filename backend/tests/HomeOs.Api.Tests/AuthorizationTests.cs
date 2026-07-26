using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HomeOs.Api.Tests;

/// <summary>
/// End-to-end negative-authZ checks against the real HTTP pipeline: protected endpoints must reject an
/// anonymous caller (401), while public ones stay open. Boots the app via <see cref="WebApplicationFactory{T}"/>.
/// </summary>
public class AuthorizationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/api/tasks")]
    [InlineData("/api/finance/summary")]
    [InlineData("/api/calendar/events")]
    [InlineData("/api/notes")]
    [InlineData("/api/reminders")]
    [InlineData("/api/shopping/lists")]
    [InlineData("/api/apps")]
    [InlineData("/api/audit")]         // manager-only — anonymous must not even get past auth
    [InlineData("/api/members")]
    [InlineData("/api/assistant/status")]
    [InlineData("/api/chat")]
    [InlineData("/api/scoreboard")]
    [InlineData("/api/households/switchable")]
    [InlineData("/api/attachments?ownerType=task&ownerId=00000000-0000-0000-0000-000000000000")]
    public async Task Protected_endpoints_reject_anonymous(string path)
    {
        var response = await Client().GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ping_is_public()
    {
        var response = await Client().GetAsync("/api/ping");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

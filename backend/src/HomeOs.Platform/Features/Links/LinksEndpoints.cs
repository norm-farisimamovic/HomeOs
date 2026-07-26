using HomeOs.Platform.Links;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HomeOs.Platform.Features.Links;

/// <summary>Cross-app links — any app object can link to any other (the "connected web").</summary>
public static class LinksEndpoints
{
    public static IEndpointRouteBuilder MapLinksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/links").RequireAuthorization().WithTags("Links");

        group.MapGet("/", async (string fromType, Guid fromId, ICurrentMember me, IEntityLinks links, CancellationToken ct) =>
        {
            var result = await links.ForAsync(me.HouseholdId, fromType, fromId, ct);
            return Results.Ok(result.Select(ToDto).ToList());
        }).WithName("ListLinks");

        group.MapPost("/", async (CreateLinkRequest req, ICurrentMember me, IEntityLinks links, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.FromType) || string.IsNullOrWhiteSpace(req.ToType))
                return Results.BadRequest();
            var link = await links.LinkAsync(me.HouseholdId, req.FromType, req.FromId,
                req.ToType, req.ToId, req.ToTitle ?? string.Empty, req.ToLink ?? "/", ct);
            return Results.Ok(ToDto(link));
        }).WithName("CreateLink");

        group.MapDelete("/{id:guid}", async (Guid id, ICurrentMember me, IEntityLinks links, CancellationToken ct) =>
        {
            await links.UnlinkAsync(me.HouseholdId, id, ct);
            return Results.NoContent();
        }).WithName("DeleteLink");

        return app;
    }

    private static LinkDto ToDto(EntityLink l) => new(l.Id, l.ToType, l.ToId, l.ToTitle, l.ToLink);
}

/// <summary>A link target as shown to the client.</summary>
public sealed record LinkDto(Guid Id, string ToType, Guid ToId, string ToTitle, string ToLink);

/// <summary>Create-link payload — the target is described by type/id + a title and deep link snapshot.</summary>
public sealed record CreateLinkRequest(string FromType, Guid FromId, string ToType, Guid ToId, string? ToTitle, string? ToLink);

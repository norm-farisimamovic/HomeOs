using HomeOs.Platform.Digest;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HomeOs.Platform.Features.Digest;

/// <summary>Lets a member send themselves the digest right now — a preview of what the scheduled email looks like.</summary>
public static class DigestEndpoints
{
    public static IEndpointRouteBuilder MapDigestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/digest/preview", async (ICurrentMember me, IDigestService digest, CancellationToken ct) =>
            {
                var sent = await digest.SendToMemberAsync(me.HouseholdId, me.Id, 7, ct);
                return Results.Ok(new DigestPreviewResponse(sent));
            })
            .RequireAuthorization().WithTags("Digest").WithName("DigestPreview");

        return app;
    }
}

/// <summary><c>Sent</c> is false when there was nothing upcoming (or the address is a demo sink).</summary>
public sealed record DigestPreviewResponse(bool Sent);

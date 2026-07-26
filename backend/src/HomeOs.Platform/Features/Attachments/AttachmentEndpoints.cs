using HomeOs.Platform.Attachments;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Features.Attachments;

/// <summary>
/// Kernel file attachments — any app can offer "attach a file" (a bill photo, a warranty PDF, a document scan)
/// by pointing at an owner type + id. Bytes are stored in the database; listings never load the blob.
/// </summary>
public static class AttachmentEndpoints
{
    private const long MaxBytes = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attachments").RequireAuthorization().WithTags("Attachments");

        // List metadata for one owner (no bytes).
        group.MapGet("/", async (string ownerType, Guid ownerId, ICurrentMember me, PlatformDbContext db, CancellationToken ct) =>
        {
            var items = await db.Attachments.AsNoTracking()
                .Where(a => a.HouseholdId == me.HouseholdId && a.OwnerType == ownerType && a.OwnerId == ownerId)
                .OrderByDescending(a => a.UploadedAtUtc)
                .Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.Size, a.UploadedById, a.UploadedAtUtc.ToString("o")))
                .ToListAsync(ct);
            return Results.Ok(items);
        }).WithName("ListAttachments");

        // Upload a file for an owner.
        group.MapPost("/", async (IFormFile? file, string ownerType, Guid ownerId,
            ICurrentMember me, PlatformDbContext db, IAppText text, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(ownerType)) return Results.BadRequest();
            if (file is null || file.Length == 0) return Results.BadRequest();
            if (file.Length > MaxBytes) return Results.Problem(statusCode: 400, title: text["error.attachment.tooLarge"]);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var name = Path.GetFileName(file.FileName);
            if (name.Length > 260) name = name[^260..];

            var attachment = new Attachment
            {
                HouseholdId = me.HouseholdId,
                OwnerType = ownerType,
                OwnerId = ownerId,
                FileName = string.IsNullOrWhiteSpace(name) ? "file" : name,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                Size = file.Length,
                Data = ms.ToArray(),
                UploadedById = me.Id,
            };
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.Size, attachment.UploadedById, attachment.UploadedAtUtc.ToString("o")));
        }).DisableAntiforgery().WithName("UploadAttachment");

        // Download one attachment's bytes.
        group.MapGet("/{id:guid}", async (Guid id, ICurrentMember me, PlatformDbContext db, CancellationToken ct) =>
        {
            var a = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == me.HouseholdId, ct);
            if (a is null) return Results.NotFound();
            return Results.File(a.Data, a.ContentType, a.FileName);
        }).WithName("DownloadAttachment");

        group.MapDelete("/{id:guid}", async (Guid id, ICurrentMember me, PlatformDbContext db, CancellationToken ct) =>
        {
            var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == me.HouseholdId, ct);
            if (a is null) return Results.NotFound();
            db.Attachments.Remove(a);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithName("DeleteAttachment");

        return app;
    }
}

/// <summary>Attachment metadata for the client (never carries the bytes).</summary>
public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, Guid UploadedById, string UploadedAt);

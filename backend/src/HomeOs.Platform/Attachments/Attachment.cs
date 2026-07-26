namespace HomeOs.Platform.Attachments;

/// <summary>
/// A file attached to some entity in another module (a task, a bill, a life-admin document…).
/// Kernel-owned so any app can offer attachments without depending on a storage module — the same way
/// avatars are stored. Bytes live in the database (longblob); fine for household-scale documents/photos.
/// </summary>
public sealed class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owning household (every query is scoped to this).</summary>
    public Guid HouseholdId { get; set; }

    /// <summary>What kind of thing this is attached to (e.g. <c>task</c>, <c>bill</c>, <c>life</c>).</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>The id of the owning entity within its module.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Original file name (for display + download).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type (e.g. <c>image/jpeg</c>, <c>application/pdf</c>).</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Size in bytes (kept alongside so listings don't load the blob).</summary>
    public long Size { get; set; }

    /// <summary>Raw file bytes.</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>Member who uploaded it.</summary>
    public Guid UploadedById { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

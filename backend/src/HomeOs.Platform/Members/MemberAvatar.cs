namespace HomeOs.Platform.Members;

/// <summary>A member's profile picture, stored in the database (one row per member who has uploaded one).</summary>
public sealed class MemberAvatar
{
    /// <summary>The member this avatar belongs to (primary key).</summary>
    public Guid MemberId { get; set; }

    /// <summary>Raw image bytes.</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>MIME type (e.g. <c>image/png</c>).</summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>When it was last set (UTC) — also used for cache-busting.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

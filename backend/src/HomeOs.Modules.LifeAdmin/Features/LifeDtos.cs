namespace HomeOs.Modules.LifeAdmin.Features;

/// <summary>A life-admin record as returned to clients.</summary>
public sealed record LifeRecordDto(
    System.Guid Id, string Title, string Category, string? ExpiresOn, int? DaysToExpiry,
    string? Provider, string? Notes, string Visibility, System.Guid OwnerId, bool CanEdit);

/// <summary>Create/update payload.</summary>
public sealed record SaveLifeRecordRequest(
    string Title, string? Category, string? ExpiresOn, string? Provider, string? Notes, string? Visibility);

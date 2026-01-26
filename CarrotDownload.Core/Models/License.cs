using CarrotDownload.Core.Enums;

namespace CarrotDownload.Core.Models;

public sealed class License
{
    public string Id { get; init; } = string.Empty;
    public LicenseStatus Status { get; init; } = LicenseStatus.Active;
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int MaxDevices { get; init; }
    public int CurrentDevices { get; init; }
    public string? Notes { get; init; }
}




using CarrotDownload.Core.Enums;

namespace CarrotDownload.Core.Models;

public sealed class DeviceBinding
{
    public string DeviceId { get; init; } = string.Empty; // e.g., hashed hardware/MAC
    public string? Name { get; init; }
    public DateTimeOffset BoundAt { get; init; }
    public DateTimeOffset? LastSeenAt { get; init; }
    public DeviceStatus Status { get; init; } = DeviceStatus.Active;
}


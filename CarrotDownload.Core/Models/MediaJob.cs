using CarrotDownload.Core.Enums;

namespace CarrotDownload.Core.Models;

public sealed class MediaJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string UserId { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public string? Preset { get; init; }
    public MediaJobType JobType { get; init; } = MediaJobType.Convert;
    public MediaJobStatus Status { get; set; } = MediaJobStatus.Queued;
    public double Progress { get; set; } // 0..100
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
    public MediaJobResult? Result { get; set; }
    public FFmpegOptions Options { get; init; } = new();
}




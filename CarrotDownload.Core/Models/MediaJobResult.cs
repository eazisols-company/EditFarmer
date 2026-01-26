namespace CarrotDownload.Core.Models;

public sealed class MediaJobResult
{
    public bool Success { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
    public double ProcessingTimeSeconds { get; init; }
    public TimeSpan? Duration { get; init; }
    public long? SizeBytes { get; init; }
    public string? LogPath { get; init; }
}



namespace CarrotDownload.Core.Models;

public sealed class QueueConfig
{
    public int MaxConcurrent { get; init; } = 1;
    public int RetryCount { get; init; } = 0;
}




namespace CarrotDownload.Core.Models;

public sealed class FFmpegOptions
{
    public string InputPath { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public int VideoBitrate { get; init; } // in kbps
    public int AudioBitrate { get; init; } // in kbps
    public int Width { get; init; }
    public int Height { get; init; }
    public int? Crf { get; init; } // Constant Rate Factor (0-51, lower = better quality)
    public string? Resolution { get; init; }
    public string? ExtraArgs { get; init; }
}



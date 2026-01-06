using CarrotDownload.Core.Models;

namespace CarrotDownload.FFmpeg.Interfaces;

public interface IFFmpegService
{
    /// <summary>
    /// Checks if FFmpeg is available and working
    /// </summary>
    Task<bool> IsFFmpegAvailableAsync();

    /// <summary>
    /// Gets FFmpeg version information
    /// </summary>
    Task<string> GetVersionAsync();

    /// <summary>
    /// Converts a media file to a different format
    /// </summary>
    Task<MediaJobResult> ConvertAsync(
        string inputPath, 
        string outputPath, 
        FFmpegOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts audio from a video file
    /// </summary>
    Task<MediaJobResult> ExtractAudioAsync(
        string inputPath,
        string outputPath,
        string audioFormat = "mp3",
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets media file information (duration, codec, resolution, etc.)
    /// </summary>
    Task<MediaFileInfo> GetMediaInfoAsync(string filePath);

    /// <summary>
    /// Compresses a video file
    /// </summary>
    Task<MediaJobResult> CompressVideoAsync(
        string inputPath,
        string outputPath,
        int quality = 23, // CRF value (0-51, lower = better quality)
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Concatenates multiple media files into a single output file
    /// </summary>
    Task<MediaJobResult> ConcatenateMediaAsync(
        List<string> inputPaths,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a thumbnail image from a video file at a specific time
    /// </summary>
    Task<string?> GenerateThumbnailAsync(string inputPath, string outputPath, TimeSpan time);
}

public sealed class MediaFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public long FileSizeBytes { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public int BitRate { get; set; }
}

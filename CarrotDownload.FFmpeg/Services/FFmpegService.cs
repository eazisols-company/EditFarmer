using CarrotDownload.Core.Enums;
using CarrotDownload.Core.Models;
using CarrotDownload.FFmpeg.Interfaces;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CarrotDownload.FFmpeg.Services;

public sealed class FFmpegService : IFFmpegService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FFmpegService()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        
        string ffmpegName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        string ffprobeName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "ffprobe.exe" : "ffprobe";
        
        // Check local bundled path first
        _ffmpegPath = Path.Combine(appDir, "ffmpeg", "bin", ffmpegName);
        _ffprobePath = Path.Combine(appDir, "ffmpeg", "bin", ffprobeName);
    }

    public async Task<bool> IsFFmpegAvailableAsync()
    {
        try
        {
            if (!File.Exists(_ffmpegPath))
                return false;

            var version = await GetVersionAsync();
            return !string.IsNullOrEmpty(version);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetVersionAsync()
    {
        try
        {
            var output = await RunFFmpegCommandAsync("-version", TimeSpan.FromSeconds(5));
            var match = Regex.Match(output, @"ffmpeg version ([\d.]+)");
            return match.Success ? match.Groups[1].Value : output.Split('\n')[0];
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<MediaJobResult> ConvertAsync(
        string inputPath,
        string outputPath,
        FFmpegOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(inputPath))
            {
                return new MediaJobResult
                {
                    Success = false,
                    ErrorMessage = "Input file not found"
                };
            }

            // Get input file duration for progress calculation
            var mediaInfo = await GetMediaInfoAsync(inputPath);
            var totalDuration = mediaInfo.Duration.TotalSeconds;

            // Build FFmpeg command
            var arguments = BuildConvertCommand(inputPath, outputPath, options);

            // Run FFmpeg with progress tracking
            var result = await RunFFmpegWithProgressAsync(
                arguments,
                totalDuration,
                progress,
                cancellationToken);

            return new MediaJobResult
            {
                Success = result.Success,
                OutputPath = result.Success ? outputPath : null,
                ErrorMessage = result.ErrorMessage,
                ProcessingTimeSeconds = result.ProcessingTime.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            return new MediaJobResult
            {
                Success = false,
                ErrorMessage = $"Conversion failed: {ex.Message}"
            };
        }
    }

    public async Task<MediaJobResult> ExtractAudioAsync(
        string inputPath,
        string outputPath,
        string audioFormat = "mp3",
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(inputPath))
            {
                return new MediaJobResult
                {
                    Success = false,
                    ErrorMessage = "Input file not found"
                };
            }

            var mediaInfo = await GetMediaInfoAsync(inputPath);
            var totalDuration = mediaInfo.Duration.TotalSeconds;

            // FFmpeg command to extract audio
            var arguments = $"-i \"{inputPath}\" -vn -acodec {GetAudioCodec(audioFormat)} -y \"{outputPath}\"";

            var result = await RunFFmpegWithProgressAsync(
                arguments,
                totalDuration,
                progress,
                cancellationToken);

            return new MediaJobResult
            {
                Success = result.Success,
                OutputPath = result.Success ? outputPath : null,
                ErrorMessage = result.ErrorMessage,
                ProcessingTimeSeconds = result.ProcessingTime.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            return new MediaJobResult
            {
                Success = false,
                ErrorMessage = $"Audio extraction failed: {ex.Message}"
            };
        }
    }

    public async Task<MediaFileInfo> GetMediaInfoAsync(string filePath)
    {
        try
        {
            var arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";
            var output = await RunFFprobeCommandAsync(arguments);

            // Parse JSON output (simplified - you may want to use System.Text.Json)
            var info = new MediaFileInfo
            {
                FileName = Path.GetFileName(filePath),
                FileSizeBytes = new FileInfo(filePath).Length
            };

            // Extract duration
            var durationMatch = Regex.Match(output, @"""duration""\s*:\s*""([\d.]+)""");
            if (durationMatch.Success && double.TryParse(durationMatch.Groups[1].Value, out var duration))
            {
                info.Duration = TimeSpan.FromSeconds(duration);
            }

            // Extract format
            var formatMatch = Regex.Match(output, @"""format_name""\s*:\s*""([^""]+)""");
            if (formatMatch.Success)
            {
                info.Format = formatMatch.Groups[1].Value;
            }

            // Extract video codec and resolution
            var videoCodecMatch = Regex.Match(output, @"""codec_name""\s*:\s*""([^""]+)"".*?""codec_type""\s*:\s*""video""");
            if (videoCodecMatch.Success)
            {
                info.VideoCodec = videoCodecMatch.Groups[1].Value;
            }

            var widthMatch = Regex.Match(output, @"""width""\s*:\s*(\d+)");
            var heightMatch = Regex.Match(output, @"""height""\s*:\s*(\d+)");
            if (widthMatch.Success && heightMatch.Success)
            {
                info.Width = int.Parse(widthMatch.Groups[1].Value);
                info.Height = int.Parse(heightMatch.Groups[1].Value);
            }

            return info;
        }
        catch
        {
            return new MediaFileInfo { FileName = Path.GetFileName(filePath) };
        }
    }

    public async Task<MediaJobResult> ConcatenateMediaAsync(
        List<string> inputPaths,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string listFilePath = Path.GetTempFileName();
        try
        {
            if (inputPaths == null || !inputPaths.Any())
            {
                return new MediaJobResult { Success = false, ErrorMessage = "No input files provided" };
            }

            // Create concat list file
            // format: file 'path'
            System.Diagnostics.Debug.WriteLine("[FFMPEG CONCAT] Creating concat list file:");
            using (var writer = new StreamWriter(listFilePath))
            {
                int idx = 0;
                foreach (var path in inputPaths)
                {
                    System.Diagnostics.Debug.WriteLine($"  [{idx++}] {Path.GetFileName(path)}");
                    // Replace backslashes with forward slashes for FFmpeg compatibility
                    // and handle special characters if necessary.
                    // Note: FFmpeg concat demuxer syntax requires escaping single quotes in paths.
                    string safePath = path.Replace("\\", "/").Replace("'", "'\\''");
                    await writer.WriteLineAsync($"file '{safePath}'");
                }
            }
            System.Diagnostics.Debug.WriteLine($"[FFMPEG CONCAT] List file created at: {listFilePath}");

            // Calculate total duration for progress
            double totalDuration = 0;
            foreach (var path in inputPaths)
            {
                var info = await GetMediaInfoAsync(path);
                totalDuration += info.Duration.TotalSeconds;
            }

            // Re-encoding ensures all segments are compatible in the output
            var arguments = $"-f concat -safe 0 -i \"{listFilePath}\" -c:v libx264 -c:a aac -y \"{outputPath}\"";

            var result = await RunFFmpegWithProgressAsync(
                arguments,
                totalDuration,
                progress,
                cancellationToken);

            return new MediaJobResult
            {
                Success = result.Success,
                OutputPath = result.Success ? outputPath : null,
                ErrorMessage = result.ErrorMessage,
                ProcessingTimeSeconds = result.ProcessingTime.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            return new MediaJobResult
            {
                Success = false,
                ErrorMessage = $"Concatenation failed: {ex.Message}"
            };
        }
        finally
        {
            if (File.Exists(listFilePath))
                File.Delete(listFilePath);
        }
    }

    public async Task<MediaJobResult> CompressVideoAsync(
        string inputPath,
        string outputPath,
        int quality = 23,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mediaInfo = await GetMediaInfoAsync(inputPath);
            var totalDuration = mediaInfo.Duration.TotalSeconds;

            // H.264 compression with CRF
            var arguments = $"-i \"{inputPath}\" -c:v libx264 -crf {quality} -c:a copy -y \"{outputPath}\"";

            var result = await RunFFmpegWithProgressAsync(
                arguments,
                totalDuration,
                progress,
                cancellationToken);

            return new MediaJobResult
            {
                Success = result.Success,
                OutputPath = result.Success ? outputPath : null,
                ErrorMessage = result.ErrorMessage,
                ProcessingTimeSeconds = result.ProcessingTime.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            return new MediaJobResult
            {
                Success = false,
                ErrorMessage = $"Compression failed: {ex.Message}"
            };
        }
    }

    // Helper methods

    public async Task<string?> GenerateThumbnailAsync(string inputPath, string outputPath, TimeSpan time)
    {
        try
        {
            if (!File.Exists(inputPath)) return null;

            // Ensure destination directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Command: ffmpeg -ss [time] -i [input] -frames:v 1 -q:v 2 [output]
            // -ss before -i is faster as it jumps to the time
            var timeStr = $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds:D3}";
            var arguments = $"-ss {timeStr} -i \"{inputPath}\" -frames:v 1 -q:v 2 -y \"{outputPath}\"";

            await RunFFmpegCommandAsync(arguments, TimeSpan.FromSeconds(10));

            return File.Exists(outputPath) ? outputPath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thumbnail generation failed: {ex.Message}");
            return null;
        }
    }

    private string BuildConvertCommand(string inputPath, string outputPath, FFmpegOptions options)
    {
        var args = new List<string>
        {
            $"-i \"{inputPath}\""
        };

        if (!string.IsNullOrEmpty(options.VideoCodec))
            args.Add($"-c:v {options.VideoCodec}");

        if (!string.IsNullOrEmpty(options.AudioCodec))
            args.Add($"-c:a {options.AudioCodec}");

        if (options.VideoBitrate > 0)
            args.Add($"-b:v {options.VideoBitrate}k");

        if (options.AudioBitrate > 0)
            args.Add($"-b:a {options.AudioBitrate}k");

        if (options.Width > 0 && options.Height > 0)
            args.Add($"-s {options.Width}x{options.Height}");

        args.Add("-y"); // Overwrite output file
        args.Add($"\"{outputPath}\"");

        return string.Join(" ", args);
    }

    private string GetAudioCodec(string format)
    {
        return format.ToLower() switch
        {
            "mp3" => "libmp3lame",
            "aac" => "aac",
            "wav" => "pcm_s16le",
            "flac" => "flac",
            _ => "copy"
        };
    }

    private async Task<(bool Success, string ErrorMessage, TimeSpan ProcessingTime)> RunFFmpegWithProgressAsync(
        string arguments,
        double totalDurationSeconds,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        var errorOutput = new System.Text.StringBuilder();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                errorOutput.AppendLine(e.Data);

                // Parse progress from FFmpeg output
                var timeMatch = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.\d+)");
                if (timeMatch.Success && totalDurationSeconds > 0)
                {
                    var hours = int.Parse(timeMatch.Groups[1].Value);
                    var minutes = int.Parse(timeMatch.Groups[2].Value);
                    var seconds = double.Parse(timeMatch.Groups[3].Value);

                    var currentSeconds = hours * 3600 + minutes * 60 + seconds;
                    var progressPercent = Math.Min(100, (currentSeconds / totalDurationSeconds) * 100);

                    progress?.Report(progressPercent);
                }
            };

            process.Start();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var processingTime = DateTime.Now - startTime;
            var success = process.ExitCode == 0;

            return (success, success ? string.Empty : errorOutput.ToString(), processingTime);
        }
        catch (OperationCanceledException)
        {
            return (false, "Operation was cancelled", DateTime.Now - startTime);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, DateTime.Now - startTime);
        }
    }

    private async Task<string> RunFFmpegCommandAsync(string arguments, TimeSpan timeout)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return string.IsNullOrEmpty(output) ? error : output;
    }

    private async Task<string> RunFFprobeCommandAsync(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }
}

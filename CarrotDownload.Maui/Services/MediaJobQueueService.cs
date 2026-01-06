using CarrotDownload.Core.Enums;
using CarrotDownload.Core.Interfaces;
using CarrotDownload.Core.Models;
using CarrotDownload.FFmpeg.Interfaces;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CarrotDownload.Maui.Services;

public sealed class MediaJobQueueService : IMediaJobQueue, IDisposable
{
    private readonly IFFmpegService _ffmpegService;
    private readonly ConcurrentDictionary<string, MediaJob> _jobs = new();
    private readonly Channel<string> _jobQueue;
    private readonly CancellationTokenSource _cts = new();
    private Task? _processingTask;

    public MediaJobQueueService(IFFmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;
        _jobQueue = Channel.CreateUnbounded<string>();
        StartProcessing();
    }

    private void StartProcessing()
    {
        _processingTask = Task.Run(ProcessQueueAsync);
    }

    public async Task<MediaJob> EnqueueAsync(MediaJob job, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(job.Id))
        {
            // Should be immutable, but if it was created without ID (unlikely due to init), set it.
            // Since it's init-only, we assume it's set.
        }

        job.Status = MediaJobStatus.Queued;
        _jobs[job.Id] = job;
        await _jobQueue.Writer.WriteAsync(job.Id, cancellationToken);
        return job;
    }

    public Task<IReadOnlyCollection<MediaJob>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyCollection<MediaJob>>(_jobs.Values.ToList());
    }

    public Task<bool> CancelAsync(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            if (job.Status == MediaJobStatus.Queued)
            {
                job.Status = MediaJobStatus.Canceled;
                return Task.FromResult(true);
            }
            // If running, we can't easily cancel the specific FFmpeg task unless we track the CTS for each running job.
            // For now, we only support cancelling queued jobs or we need to implement running job cancellation.
            // Since we process one at a time, we could have a current job CTS.
        }
        return Task.FromResult(false);
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var jobId in _jobQueue.Reader.ReadAllAsync(_cts.Token))
            {
                if (!_jobs.TryGetValue(jobId, out var job)) continue;
                if (job.Status == MediaJobStatus.Canceled) continue;

                try
                {
                    job.Status = MediaJobStatus.Running;
                    job.StartedAt = DateTimeOffset.UtcNow;
                    job.Progress = 0;

                    var progress = new Progress<double>(p => job.Progress = p);
                    MediaJobResult result;

                    switch (job.JobType)
                    {
                        case MediaJobType.ExtractAudio:
                            result = await _ffmpegService.ExtractAudioAsync(
                                job.SourcePath,
                                job.OutputPath,
                                job.Options.AudioCodec ?? "mp3",
                                progress,
                                _cts.Token);
                            break;

                        case MediaJobType.Compress:
                            result = await _ffmpegService.CompressVideoAsync(
                                job.SourcePath,
                                job.OutputPath,
                                job.Options.Crf ?? 23,
                                progress,
                                _cts.Token);
                            break;

                        case MediaJobType.Convert:
                        default:
                            result = await _ffmpegService.ConvertAsync(
                                job.SourcePath,
                                job.OutputPath,
                                job.Options,
                                progress,
                                _cts.Token);
                            break;
                    }

                    job.Result = result;
                    job.Status = result.Success ? MediaJobStatus.Succeeded : MediaJobStatus.Failed;
                    job.CompletedAt = DateTimeOffset.UtcNow;
                    
                    if (!result.Success)
                    {
                        job.Error = result.ErrorMessage;
                    }
                }
                catch (Exception ex)
                {
                    job.Status = MediaJobStatus.Failed;
                    job.Error = ex.Message;
                    job.CompletedAt = DateTimeOffset.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Service stopping
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

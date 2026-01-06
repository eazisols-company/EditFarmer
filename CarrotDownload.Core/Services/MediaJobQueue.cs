using System.Collections.Concurrent;
using CarrotDownload.Core.Enums;
using CarrotDownload.Core.Interfaces;
using CarrotDownload.Core.Models;

namespace CarrotDownload.Core.Services;

/// <summary>
/// Simple single-concurrency job queue. Processing hook to be provided by a runner in the FFmpeg layer.
/// </summary>
public sealed class MediaJobQueue : IMediaJobQueue
{
    private readonly ConcurrentQueue<MediaJob> _queue = new();
    private readonly ConcurrentDictionary<string, MediaJob> _jobs = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly QueueConfig _config;
    private readonly Func<MediaJob, CancellationToken, Task> _processor;

    public MediaJobQueue(QueueConfig config, Func<MediaJob, CancellationToken, Task> processor)
    {
        _config = config;
        _processor = processor;
    }

    public Task<IReadOnlyCollection<MediaJob>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyCollection<MediaJob>>(_jobs.Values.ToList());
    }

    public Task<MediaJob> EnqueueAsync(MediaJob job, CancellationToken cancellationToken = default)
    {
        _jobs[job.Id] = job;
        _queue.Enqueue(job);
        _ = ProcessNextAsync(cancellationToken);
        return Task.FromResult(job);
    }

    public Task<bool> CancelAsync(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = MediaJobStatus.Canceled;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    private async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        // Ensure only one processor loop runs at a time.
        if (!await _gate.WaitAsync(0, cancellationToken)) return;

        try
        {
            while (_queue.TryDequeue(out var job))
            {
                if (job.Status == MediaJobStatus.Canceled)
                {
                    continue;
                }

                job.Status = MediaJobStatus.Running;
                job.StartedAt ??= DateTimeOffset.UtcNow;

                try
                {
                    await _processor(job, cancellationToken);
                    if (job.Status != MediaJobStatus.Canceled)
                    {
                        job.Status = MediaJobStatus.Succeeded;
                        job.CompletedAt = DateTimeOffset.UtcNow;
                    }
                }
                catch (OperationCanceledException)
                {
                    job.Status = MediaJobStatus.Canceled;
                }
                catch (Exception ex)
                {
                    job.Status = MediaJobStatus.Failed;
                    job.Error = ex.Message;
                    job.CompletedAt = DateTimeOffset.UtcNow;
                }

                // Respect single-concurrency; if MaxConcurrent > 1 later, adjust to parallel runners.
                if (_config.MaxConcurrent <= 1 && _queue.IsEmpty)
                {
                    break;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}


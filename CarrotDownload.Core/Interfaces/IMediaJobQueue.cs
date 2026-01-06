using CarrotDownload.Core.Models;

namespace CarrotDownload.Core.Interfaces;

public interface IMediaJobQueue
{
    Task<MediaJob> EnqueueAsync(MediaJob job, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MediaJob>> GetAllAsync();
    Task<bool> CancelAsync(string jobId);
}


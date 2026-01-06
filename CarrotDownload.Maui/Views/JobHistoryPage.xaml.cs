using CarrotDownload.Core.Enums;
using CarrotDownload.Core.Interfaces;
using CarrotDownload.Core.Models;
using System.Collections.ObjectModel;

namespace CarrotDownload.Maui.Views;

public partial class JobHistoryPage : ContentPage
{
    private readonly IMediaJobQueue _jobQueue;
    private readonly ObservableCollection<JobViewModel> _jobs = new();
    private System.Timers.Timer? _refreshTimer;

    public JobHistoryPage(IMediaJobQueue jobQueue)
    {
        InitializeComponent();
        _jobQueue = jobQueue;
        JobsCollection.ItemsSource = _jobs;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadJobs();
        
        // Auto-refresh every second to show progress
        _refreshTimer = new System.Timers.Timer(1000);
        _refreshTimer.Elapsed += (s, e) => MainThread.BeginInvokeOnMainThread(LoadJobs);
        _refreshTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    private async void LoadJobs()
    {
        var jobs = await _jobQueue.GetAllAsync();
        
        // Simple reload for now (in production, we'd update existing items to avoid flicker)
        _jobs.Clear();
        foreach (var job in jobs.OrderByDescending(j => j.CreatedAt))
        {
            _jobs.Add(new JobViewModel(job));
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void OnRefreshClicked(object sender, EventArgs e)
    {
        LoadJobs();
    }
}

public class JobViewModel
{
    private readonly MediaJob _job;

    public JobViewModel(MediaJob job)
    {
        _job = job;
    }

    public string JobType => _job.JobType.ToString();
    public string Status => _job.Status.ToString();
    public string SourceFileName => Path.GetFileName(_job.SourcePath);
    public double ProgressPercent => _job.Progress / 100.0;
    public string? Error => _job.Error;
    public bool HasError => !string.IsNullOrEmpty(_job.Error);
    public bool IsRunning => _job.Status == MediaJobStatus.Running;

    public Color StatusColor => _job.Status switch
    {
        MediaJobStatus.Queued => Colors.Gray,
        MediaJobStatus.Running => Color.FromArgb("#2196F3"), // Blue
        MediaJobStatus.Succeeded => Color.FromArgb("#4CAF50"), // Green
        MediaJobStatus.Failed => Colors.Red,
        MediaJobStatus.Canceled => Colors.Orange,
        _ => Colors.Gray
    };
}

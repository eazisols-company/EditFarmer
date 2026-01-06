using CarrotDownload.Core.Enums;
using CarrotDownload.Core.Interfaces;
using CarrotDownload.Core.Models;
using CarrotDownload.FFmpeg.Interfaces;
using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class MediaProcessingPage : ContentPage
{
	private readonly IFFmpegService _ffmpegService;
	private readonly IMediaJobQueue _jobQueue;
	private string? _selectedFilePath;
	private string? _outputFolderPath;

	public MediaProcessingPage(IFFmpegService ffmpegService, IMediaJobQueue jobQueue)
	{
		InitializeComponent();
		_ffmpegService = ffmpegService;
		_jobQueue = jobQueue;
		CheckFFmpegAvailability();
	}

	private async void CheckFFmpegAvailability()
	{
		var isAvailable = await _ffmpegService.IsFFmpegAvailableAsync();
		if (!isAvailable)
		{
			await NotificationService.ShowError("FFmpeg is not available. Please ensure FFmpeg binaries are included in the app.");
		}
		else
		{
			var version = await _ffmpegService.GetVersionAsync();
			System.Diagnostics.Debug.WriteLine($"FFmpeg version: {version}");
		}
	}

	private void OnDragOver(object sender, DragEventArgs e)
	{
		e.AcceptedOperation = DataPackageOperation.Copy;

#if WINDOWS
        if (e.PlatformArgs?.DragEventArgs != null)
        {
            e.PlatformArgs.DragEventArgs.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.PlatformArgs.DragEventArgs.DragUIOverride.IsCaptionVisible = true;
            e.PlatformArgs.DragEventArgs.DragUIOverride.Caption = "Drop to select file";
        }
#endif

		FileSelectionBorder.Stroke = Color.FromArgb("#2196F3"); // Blue highlight
		FileSelectionBorder.BackgroundColor = Color.FromArgb("#e3f2fd"); // Light blue background
	}

	private void OnDragLeave(object sender, DragEventArgs e)
	{
		FileSelectionBorder.Stroke = Color.FromArgb("#e5e5e5");
		FileSelectionBorder.BackgroundColor = Colors.White;
	}

	private async void OnDrop(object sender, DropEventArgs e)
	{
		OnDragLeave(sender, null!); // Reset style

		try
		{
			string? firstPath = null;

#if WINDOWS
            if (e.PlatformArgs?.DragEventArgs?.DataView != null)
            {
                var dataView = e.PlatformArgs.DragEventArgs.DataView;
                if (dataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                {
                    var items = await dataView.GetStorageItemsAsync();
                    var firstItem = items.FirstOrDefault();
                    if (firstItem != null && !string.IsNullOrEmpty(firstItem.Path))
                    {
                        firstPath = firstItem.Path;
                    }
                }
            }
#endif

			// Fallback to MAUI properties if platform args didn't provide path
			if (string.IsNullOrEmpty(firstPath) && e.Data.Properties.ContainsKey("FileNames"))
			{
				var paths = e.Data.Properties["FileNames"] as IEnumerable<string>;
				firstPath = paths?.FirstOrDefault();
			}

			if (!string.IsNullOrEmpty(firstPath))
			{
				await ProcessSelectedFile(firstPath);
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Error dropping file: {ex.Message}");
		}
	}

	private async void OnSelectFileClicked(object sender, EventArgs e)
	{
		try
		{
			var results = await FilePicker.PickMultipleAsync(new PickOptions
			{
				PickerTitle = "Select media files",
				FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
				{
					{ DevicePlatform.WinUI, new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".mp3", ".wav", ".aac", ".flac" } }
				})
			});

			if (results != null && results.Any())
			{
				// Media processing currently handles one file at a time, process the first one
				await ProcessSelectedFile(results.First().FullPath);
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to select file: {ex.Message}");
		}
	}

	private async Task ProcessSelectedFile(string filePath)
	{
		_selectedFilePath = filePath;
		FileNameLabel.Text = Path.GetFileName(filePath);
		
		var fileInfo = new FileInfo(_selectedFilePath);
		FileSizeLabel.Text = $"Size: {FormatFileSize(fileInfo.Length)}";
		
		FileInfoPanel.IsVisible = true;
		UpdateStartButtonState();

		// Get media info
		var mediaInfo = await _ffmpegService.GetMediaInfoAsync(_selectedFilePath);
		if (mediaInfo.Duration > TimeSpan.Zero)
		{
			FileSizeLabel.Text += $" • Duration: {mediaInfo.Duration:mm\\:ss}";
		}

		// Generate thumbnail
		try
		{
			var ext = Path.GetExtension(filePath).ToLower();
			var isVideo = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" }.Contains(ext);
			
			if (isVideo)
			{
				var thumbPath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.jpg");
				var generatedPath = await _ffmpegService.GenerateThumbnailAsync(filePath, thumbPath, TimeSpan.FromSeconds(1));
				
				if (!string.IsNullOrEmpty(generatedPath) && File.Exists(generatedPath))
				{
					FileThumbnailImage.Source = ImageSource.FromFile(generatedPath);
				}
				else
				{
					FileThumbnailImage.Source = null;
				}
			}
			else
			{
				FileThumbnailImage.Source = null;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Thumbnail generation failed: {ex.Message}");
			FileThumbnailImage.Source = null;
		}
	}

	private async void OnSelectOutputFolderClicked(object sender, EventArgs e)
	{
		// For now, use the same folder as input file
		// TODO: Implement proper folder picker when available in MAUI
		var message = "Output files will be saved in the same folder as the input file.\n\n" +
		              "You can change this after processing by moving the file.";
		await NotificationService.ShowInfo(message);
	}

	private void OnOperationChanged(object sender, EventArgs e)
	{
		var selectedIndex = OperationPicker.SelectedIndex;
		
		// Hide all panels first
		FormatPanel.IsVisible = false;
		AudioFormatPanel.IsVisible = false;
		QualityPanel.IsVisible = false;

		// Show relevant panel based on selection
		switch (selectedIndex)
		{
			case 0: // Convert Video Format
				FormatPanel.IsVisible = true;
				FormatPicker.SelectedIndex = 0; // Default to MP4
				break;
			case 1: // Extract Audio
				AudioFormatPanel.IsVisible = true;
				AudioFormatPicker.SelectedIndex = 0; // Default to MP3
				break;
			case 2: // Compress Video
				QualityPanel.IsVisible = true;
				break;
		}

		UpdateStartButtonState();
	}

	private void OnQualityChanged(object sender, ValueChangedEventArgs e)
	{
		var value = (int)e.NewValue;
		QualityLabel.Text = $"Quality: {value} {(value == 23 ? "(Recommended)" : value < 23 ? "(Higher Quality)" : "(Lower Quality)")}";
	}

	private void UpdateStartButtonState()
	{
		StartButton.IsEnabled = !string.IsNullOrEmpty(_selectedFilePath) && 
		                        OperationPicker.SelectedIndex >= 0;
	}

	private async void OnStartProcessingClicked(object sender, EventArgs e)
	{
		if (string.IsNullOrEmpty(_selectedFilePath))
			return;

		try
		{
			// Determine output path
			var outputFolder = _outputFolderPath ?? Path.GetDirectoryName(_selectedFilePath)!;
			var inputFileName = Path.GetFileNameWithoutExtension(_selectedFilePath);
			string outputPath;
			MediaJobType jobType;
			FFmpegOptions options = new FFmpegOptions();

			switch (OperationPicker.SelectedIndex)
			{
				case 0: // Convert Video Format
					var format = FormatPicker.SelectedItem?.ToString()?.ToLower() ?? "mp4";
					outputPath = Path.Combine(outputFolder, $"{inputFileName}_converted.{format}");
					jobType = MediaJobType.Convert;
					// Basic conversion options (can be expanded)
					break;

				case 1: // Extract Audio
					var audioFormat = AudioFormatPicker.SelectedItem?.ToString()?.ToLower() ?? "mp3";
					outputPath = Path.Combine(outputFolder, $"{inputFileName}_audio.{audioFormat}");
					jobType = MediaJobType.ExtractAudio;
					options = new FFmpegOptions { AudioCodec = audioFormat };
					break;

				case 2: // Compress Video
					var quality = (int)QualitySlider.Value;
					outputPath = Path.Combine(outputFolder, $"{inputFileName}_compressed.mp4");
					jobType = MediaJobType.Compress;
					options = new FFmpegOptions { Crf = quality };
					break;

				default:
					return;
			}

			// Create Job
			var job = new MediaJob
			{
				SourcePath = _selectedFilePath,
				OutputPath = outputPath,
				JobType = jobType,
				Options = options,
				UserId = "current-user" // TODO: Get actual user ID
			};

			// Enqueue Job
			await _jobQueue.EnqueueAsync(job);

			await NotificationService.ShowInfo("Your job has been added to the queue and will start processing shortly.");
			
			// Optional: Navigate back or clear selection
			_selectedFilePath = null;
			FileNameLabel.Text = "";
			FileSizeLabel.Text = "";
			FileInfoPanel.IsVisible = false;
			UpdateStartButtonState();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to queue job: {ex.Message}");
		}
	}

	private void OnCancelClicked(object sender, EventArgs e)
	{
		// Not used in queue mode yet
	}

	private async void OnBackTapped(object sender, EventArgs e)
	{
		await Navigation.PopAsync();
	}

	private static string FormatFileSize(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB", "TB" };
		double len = bytes;
		int order = 0;
		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len = len / 1024;
		}
		return $"{len:0.##} {sizes[order]}";
	}
}

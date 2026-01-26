using CarrotDownload.Maui.Services;
using System.Linq;
using Microsoft.Maui.Controls;

namespace CarrotDownload.Maui.Views;

[QueryProperty(nameof(FilePath), "filePath")]
public partial class MediaPlayerPage : ContentPage
{
	private string _filePath;
	private bool _isVideo;
	
	// Video file extensions
	private static readonly string[] _videoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" };
	
	// Audio file extensions
	private static readonly string[] _audioExtensions = { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg" };

	public string FilePath
	{
		get => _filePath;
		set
		{
			_filePath = value;
			LoadMediaFile();
		}
	}

	public MediaPlayerPage()
	{
		InitializeComponent();
	}

	private void LoadMediaFile()
	{
		try
		{
			// Validate file exists
			if (string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath))
			{
				ShowError("File not found. It may have been moved or deleted.");
				return;
			}

			// Extract file name for display
			var fileName = Path.GetFileName(_filePath);
			FileNameLabel.Text = fileName;

			// Detect file type
			var ext = Path.GetExtension(_filePath).ToLower();
			_isVideo = _videoExtensions.Contains(ext);

#if WINDOWS
			NativePlayer.FilePath = _filePath;
			NativePlayer.IsVisible = true;
#endif

			ErrorLayout.IsVisible = false;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading media file: {ex.Message}");
			ShowError($"Could not load the media file: {ex.Message}");
		}
	}

	private void ShowError(string message)
	{
		ErrorMessageLabel.Text = message;
		ErrorLayout.IsVisible = true;

#if WINDOWS
		NativePlayer.IsVisible = false;
#endif
	}

	private async void OnOpenExternalClicked(object sender, EventArgs e)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath))
			{
				await Launcher.Default.OpenAsync(new OpenFileRequest
				{
					File = new ReadOnlyFile(_filePath)
				});
			}
		}
		catch
		{
			await NotificationService.ShowError("We couldn't open that file.");
		}
	}

	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("..");
	}
}

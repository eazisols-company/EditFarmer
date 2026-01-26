using CarrotDownload.Maui.Services;
using System.IO;

namespace CarrotDownload.Maui.Controls;

public partial class MediaPreviewCard : ContentView
{
	public static readonly BindableProperty FilePathProperty =
		BindableProperty.Create(
			nameof(FilePath),
			typeof(string),
			typeof(MediaPreviewCard),
			default(string),
			propertyChanged: OnFilePathChanged);

	public static readonly BindableProperty FileNameProperty =
		BindableProperty.Create(
			nameof(FileName),
			typeof(string),
			typeof(MediaPreviewCard),
			default(string),
			propertyChanged: OnFileNameChanged);

	public static readonly BindableProperty FileSizeProperty =
		BindableProperty.Create(
			nameof(FileSize),
			typeof(string),
			typeof(MediaPreviewCard),
			default(string),
			propertyChanged: OnFileSizeChanged);

	public string FilePath
	{
		get => (string)GetValue(FilePathProperty);
		set => SetValue(FilePathProperty, value);
	}

	public string FileName
	{
		get => (string)GetValue(FileNameProperty);
		set => SetValue(FileNameProperty, value);
	}

	public string FileSize
	{
		get => (string)GetValue(FileSizeProperty);
		set => SetValue(FileSizeProperty, value);
	}

	public MediaPreviewCard()
	{
		InitializeComponent();
	}

	static void OnFilePathChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is not MediaPreviewCard card)
			return;

		var path = newValue as string;
		
		if (!string.IsNullOrWhiteSpace(path))
		{
			// Use dispatcher to ensure UI is ready
			card.Dispatcher.Dispatch(() =>
			{
#if WINDOWS
				try
				{
					// Additional null check before setting FilePath
					if (card.PreviewPlayer != null && !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
					{
						card.PreviewPlayer.FilePath = path;
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[MediaPreviewCard] Error setting FilePath: {ex.Message}");
				}
#endif
			});
		}
		else
		{
			// Clear the player if path is null/empty
			card.Dispatcher.Dispatch(() =>
			{
#if WINDOWS
				try
				{
					if (card.PreviewPlayer != null)
					{
						card.PreviewPlayer.FilePath = string.Empty;
					}
				}
				catch
				{
					// Ignore errors when clearing
				}
#endif
			});
		}
	}

	static void OnFileNameChanged(BindableObject bindable, object oldValue, object newValue)
	{
		// FileName label removed from UI
	}

	static void OnFileSizeChanged(BindableObject bindable, object oldValue, object newValue)
	{
		// FileSize label removed from UI
	}

	private async void OnOpenInPlayerClicked(object sender, EventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath))
		{
			var encodedPath = Uri.EscapeDataString(FilePath);
			await Shell.Current.GoToAsync($"MediaPlayerPage?filePath={encodedPath}");
		}
	}

	private async void OnFullscreenClicked(object sender, EventArgs e)
	{
		// Open in fullscreen player
		if (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath))
		{
			var encodedPath = Uri.EscapeDataString(FilePath);
			await Shell.Current.GoToAsync($"MediaPlayerPage?filePath={encodedPath}");
		}
	}
}

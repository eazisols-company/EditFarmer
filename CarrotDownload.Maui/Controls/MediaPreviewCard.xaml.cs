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
		var card = (MediaPreviewCard)bindable;
		var path = (string)newValue;
		
		if (!string.IsNullOrWhiteSpace(path))
		{
			// Use dispatcher to ensure UI is ready
			card.Dispatcher.Dispatch(() =>
			{
#if WINDOWS
				try
				{
					if (card.PreviewPlayer != null)
					{
						card.PreviewPlayer.FilePath = path;
					}
				}
				catch
				{
					// Control might not be initialized yet
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

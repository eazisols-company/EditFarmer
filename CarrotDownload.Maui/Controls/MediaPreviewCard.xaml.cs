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

    private static readonly HashSet<string> _codeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xml", ".xaml", ".json", ".c", ".cpp", ".h", ".py", ".js", ".ts", ".html", ".css", ".java", ".txt", ".md", ".sql", ".sh", ".bat", ".ps1", ".ini", ".config", ".razor"
    };

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
		Unloaded += OnUnloaded;
	}

	private void OnUnloaded(object? sender, EventArgs e)
	{
		// Stop playback and clear player when control is unloaded
		// This is critical for when items are removed from collections (delete action)
		try
		{
			TextPreviewContainer.IsVisible = false;
#if WINDOWS
			if (PreviewPlayer != null)
			{
				PreviewPlayer.FilePath = null;
			}
#endif
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[MediaPreviewCard] Unloaded cleanup error: {ex.Message}");
		}
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
                try
                {
                    bool isCode = false;
                    if (File.Exists(path))
                    {
                         var ext = Path.GetExtension(path);
                         isCode = _codeExtensions.Contains(ext);
                    }

                    if (isCode)
                    {
                        // Show text preview
                        try 
                        {
                            var lines = File.ReadLines(path).Take(40);
                            card.TextPreviewLabel.Text = string.Join(Environment.NewLine, lines);
                            card.FileTypeLabel.Text = Path.GetExtension(path).TrimStart('.').ToUpper();
                            card.TextPreviewContainer.IsVisible = true;
                            
                            // Hide players
                            if (card.PreviewPlayer != null) card.PreviewPlayer.IsVisible = false;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MediaPreview] Error reading text: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Restore specific players visibility based on platform
                        card.TextPreviewContainer.IsVisible = false;
#if WINDOWS
                        if (card.PreviewPlayer != null) card.PreviewPlayer.IsVisible = true;
#endif

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
                    }
                }
                catch (Exception ex)
                {
                     System.Diagnostics.Debug.WriteLine($"[MediaPreview] Error: {ex.Message}");
                }
			});
		}
		else
		{
			// Clear the player if path is null/empty
			card.Dispatcher.Dispatch(() =>
			{
                card.TextPreviewContainer.IsVisible = false;
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

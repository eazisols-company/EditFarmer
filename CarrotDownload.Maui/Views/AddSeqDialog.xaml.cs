using CarrotDownload.Maui.Services;
using CarrotDownload.FFmpeg.Interfaces;
using System.Linq;

namespace CarrotDownload.Maui.Views;

public partial class AddSeqDialog : ContentPage
{
	private string _selectedFilePath;
	private List<int> _existingSlots;
	private readonly IFFmpegService _ffmpegService;

	private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".mp3", ".wav", ".mp4", ".mkv", ".avi", ".mov", ".flac", ".aac"
	};

	public AddSeqDialog(List<int> existingSlots, IFFmpegService ffmpegService)
	{
		InitializeComponent();
		_existingSlots = existingSlots;
		_ffmpegService = ffmpegService;
	}

	public event EventHandler<PlaylistItemAddedEventArgs> PlaylistItemAdded;

	private async void OnSelectFileClicked(object sender, EventArgs e)
	{
		try
		{
			var options = new PickOptions
			{
				PickerTitle = "Select Audio or Video File",
				FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
				{
					{ DevicePlatform.iOS, new[] { "public.audio", "public.movie" } },
					{ DevicePlatform.Android, new[] { "audio/*", "video/*" } },
					{ DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".mp4", ".mkv", ".avi", ".mov", ".flac", ".aac" } }
				})
			};

			var result = await FilePicker.Default.PickAsync(options);
			if (result != null)
			{
				if (!IsValidMediaFile(result.FullPath))
				{
					await NotificationService.ShowError("Please select an audio or video file. Other file types aren't supported.");
					return;
				}

				SetSelectedFile(result.FullPath);
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't select that file. Please try again.");
		}
	}

	private async void OnFilesDropped(object sender, DropEventArgs e)
	{
		try
		{
			var paths = await GetPathsFromDrop(e);
			if (paths == null || !paths.Any())
			{
				return;
			}

			var distinct = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();

			// Enforce single-file drop per block
			if (distinct.Count > 1)
			{
				await NotificationService.ShowError("Please add only one file at a time to this area.");
				return;
			}

			var filePath = distinct.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || !IsValidMediaFile(filePath))
			{
				await NotificationService.ShowError("Please select an audio or video file. Other file types aren't supported.");
				return;
			}

			// Single file drop: behave like file picker and show preview so user can
			// optionally assign a slot before adding.
			SetSelectedFile(filePath);
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't add those files. Please try again.");
		}
	}

	private async Task<IEnumerable<string>> GetPathsFromDrop(DropEventArgs e)
	{
		var paths = new List<string>();

#if WINDOWS
		try
		{
			if (e.PlatformArgs?.DragEventArgs?.DataView != null)
			{
				var dataView = e.PlatformArgs.DragEventArgs.DataView;
				if (dataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
				{
					var items = await dataView.GetStorageItemsAsync();
					foreach (var item in items)
					{
						if (!string.IsNullOrEmpty(item.Path))
						{
							paths.Add(item.Path);
						}
					}
				}
			}

			// Fallback to MAUI properties if platform args didn't provide paths
			if (!paths.Any() && e.Data.Properties.ContainsKey("FileNames"))
			{
				var fileNames = e.Data.Properties["FileNames"] as IEnumerable<string>;
				if (fileNames != null) paths.AddRange(fileNames);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting paths from drop: {ex}");
		}
#else
		if (e.Data.Properties.ContainsKey("FileNames"))
		{
			var fileNames = e.Data.Properties["FileNames"] as IEnumerable<string>;
			if (fileNames != null) paths.AddRange(fileNames);
		}
#endif
		return paths;
	}

	private async void OnAddSeqClicked(object sender, EventArgs e)
	{
		// Validate file selection
		if (string.IsNullOrEmpty(_selectedFilePath))
		{
			await NotificationService.ShowError("Invalid file type. Only audio or video files are allowed.");
			return;
		}

		if (!IsValidMediaFile(_selectedFilePath))
		{
			await NotificationService.ShowError("Invalid file type. Only audio or video files are allowed.");
			return;
		}

		// Slot position is OPTIONAL here. If provided, validate and enforce uniqueness;
		// if left blank, the file is added without a slot and can be slotted later.
		int slotIndex = -1;
		string slotLetter = string.Empty;

		if (!string.IsNullOrWhiteSpace(SlotPositionEntry.Text))
		{
			string slotPositionOriginal = SlotPositionEntry.Text.Trim();
			// Check for uppercase letters
			if (slotPositionOriginal.Any(char.IsUpper))
			{
				await NotificationService.ShowError("Please use lowercase letters (a-z) for slots. Capital letters aren't allowed.");
				return;
			}
			string slotPosition = slotPositionOriginal.ToLower();
			if (slotPosition.Length != 1 || slotPosition[0] < 'a' || slotPosition[0] > 'z')
			{
				await NotificationService.ShowError("Slots should be a single letter from a to z.");
				return;
			}

			// Check if slot already exists (convert to index: a=0, b=1, etc.)
			slotIndex = slotPosition[0] - 'a';
			if (_existingSlots.Contains(slotIndex))
			{
				await NotificationService.ShowError($"The slot '{slotPosition}' is already in use. Please choose a different one.");
				return;
			}

			slotLetter = slotPosition;
		}

		// Get privacy setting
		bool isPrivate = PrivateRadio.IsChecked;

		// Raise event with the new playlist item (slot may be empty)
		PlaylistItemAdded?.Invoke(this, new PlaylistItemAddedEventArgs
		{
			FilePath = _selectedFilePath,
			FileName = Path.GetFileName(_selectedFilePath),
			SlotPosition = slotIndex,
			SlotLetter = slotLetter,
			IsPrivate = isPrivate
		});

		// Ensure subsequent adds in the same dialog session also respect uniqueness
		// for any slot that was actually provided.
		if (slotIndex >= 0)
		{
			_existingSlots.Add(slotIndex);
		}

		// Reset form for next entry
		ClearSelectedFile();
		SlotPositionEntry.Text = string.Empty;

		await NotificationService.ShowSuccess(
			string.IsNullOrEmpty(slotLetter)
				? "File added to playlist"
				: $"File added to slot {slotLetter}");
	}

	private async void OnPlaySelectedClicked(object sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(_selectedFilePath))
		{
			await NotificationService.ShowInfo("Please select a file to continue.");
			return;
		}

		try
		{
			await Launcher.Default.OpenAsync(new OpenFileRequest
			{
				File = new ReadOnlyFile(_selectedFilePath)
			});
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't play that file. Please check that it exists.");
		}
	}

	private void OnClearSelectedFileClicked(object sender, EventArgs e)
	{
		ClearSelectedFile();
	}

	private void SetSelectedFile(string fullPath)
	{
		_selectedFilePath = fullPath;
		SelectedFileLabel.Text = $"Selected: {Path.GetFileName(fullPath)}";
		SelectedFileLabel.TextColor = Color.FromArgb("#28a745");

		// Toggle UI: hide drop zone, show preview
		if (DropZoneBorder != null) DropZoneBorder.IsVisible = false;
		if (SelectedPreviewBorder != null) SelectedPreviewBorder.IsVisible = true;

		// Generate thumbnail (best-effort)
		_ = Task.Run(async () =>
		{
			try
			{
				var ext = Path.GetExtension(fullPath).ToLower();
				var isVideo = new[] { ".mp4", ".mkv", ".avi", ".mov" }.Contains(ext);
				if (!isVideo) return;

				var thumbPath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.jpg");
				var generatedPath = await _ffmpegService.GenerateThumbnailAsync(fullPath, thumbPath, TimeSpan.FromSeconds(1));
				if (!string.IsNullOrEmpty(generatedPath) && File.Exists(generatedPath))
				{
					MainThread.BeginInvokeOnMainThread(() =>
					{
						if (SelectedThumbnailImage != null)
						{
							SelectedThumbnailImage.Source = ImageSource.FromFile(generatedPath);
						}
					});
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AddSeqDialog] Thumbnail generation failed: {ex.Message}");
			}
		});
	}

	private void ClearSelectedFile()
	{
		_selectedFilePath = null;
		SelectedFileLabel.Text = "No file selected";
		SelectedFileLabel.TextColor = Color.FromArgb("#666");

		// Toggle UI: show drop zone, hide preview
		if (DropZoneBorder != null) DropZoneBorder.IsVisible = true;
		if (SelectedPreviewBorder != null) SelectedPreviewBorder.IsVisible = false;
		if (SelectedThumbnailImage != null) SelectedThumbnailImage.Source = null;
	}

	private bool IsValidMediaFile(string filePath)
	{
		try
		{
			var ext = Path.GetExtension(filePath);
			return !string.IsNullOrWhiteSpace(ext) && _allowedExtensions.Contains(ext);
		}
		catch
		{
			return false;
		}
	}

	private async void OnCancelClicked(object sender, EventArgs e)
	{
		await Navigation.PopModalAsync();
	}
}

public class PlaylistItemAddedEventArgs : EventArgs
{
	public string FilePath { get; set; }
	public string FileName { get; set; }
	public int SlotPosition { get; set; }
	public string SlotLetter { get; set; }
	public bool IsPrivate { get; set; }
}

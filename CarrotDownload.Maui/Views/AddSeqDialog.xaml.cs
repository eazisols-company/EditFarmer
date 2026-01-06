using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class AddSeqDialog : ContentPage
{
	private string _selectedFilePath;
	private List<int> _existingSlots;

	public AddSeqDialog(List<int> existingSlots)
	{
		InitializeComponent();
		_existingSlots = existingSlots;
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
				_selectedFilePath = result.FullPath;
				SelectedFileLabel.Text = $"Selected: {Path.GetFileName(result.FullPath)}";
				SelectedFileLabel.TextColor = Color.FromArgb("#28a745");
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Error selecting file: {ex.Message}");
		}
	}

	private async void OnAddSeqClicked(object sender, EventArgs e)
	{
		// Validate file selection
		if (string.IsNullOrEmpty(_selectedFilePath))
		{
			await NotificationService.ShowError("Please select a file first");
			return;
		}

		// Validate slot position
		if (string.IsNullOrWhiteSpace(SlotPositionEntry.Text))
		{
			await NotificationService.ShowError("Please enter a slot position (a-z)");
			return;
		}

		string slotPosition = SlotPositionEntry.Text.Trim().ToLower(); // Use lowercase
		
		// Validate slot is a-z
		if (slotPosition.Length != 1 || slotPosition[0] < 'a' || slotPosition[0] > 'z')
		{
			await NotificationService.ShowError("Slot position must be a single letter from a to z");
			return;
		}

	
	// Check if slot already exists (convert to index: a=0, b=1, etc.)
	int slotIndex = slotPosition[0] - 'a';
	
	if (_existingSlots.Contains(slotIndex))
	{
		await NotificationService.ShowError($"Slot '{slotPosition}' already exists in this playlist");
		return;
	}

		// Get privacy setting
		bool isPrivate = PrivateRadio.IsChecked;

		// Raise event with the new playlist item
		PlaylistItemAdded?.Invoke(this, new PlaylistItemAddedEventArgs
		{
			FilePath = _selectedFilePath,
			FileName = Path.GetFileName(_selectedFilePath),
			SlotPosition = slotIndex,
			SlotLetter = slotPosition,
			IsPrivate = isPrivate
		});

		// Reset form for next entry
		_selectedFilePath = null;
		SelectedFileLabel.Text = "No file selected";
		SelectedFileLabel.TextColor = Color.FromArgb("#666");
		SlotPositionEntry.Text = "";

		await NotificationService.ShowSuccess($"File added to slot {slotPosition}");
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

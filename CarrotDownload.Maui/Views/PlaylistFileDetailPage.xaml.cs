using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Database;

using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class PlaylistFileDetailPage : ContentPage
{
	private readonly IAuthService _authService;
	private readonly CarrotMongoService _mongoService;
	private readonly CarrotDownload.FFmpeg.Interfaces.IFFmpegService _ffmpegService;
	private string _playlistId;
	private string _projectId;
	private string _filePath;
	private string _fileName;
	private int _sequencePosition;
	
	public PlaylistFileDetailPage(
		IAuthService authService,
		CarrotMongoService mongoService,
		CarrotDownload.FFmpeg.Interfaces.IFFmpegService ffmpegService,
		string projectId,
		string playlistId,
		string fileName,
		string filePath,
		int sequencePosition,
		string slotPosition,
		DateTime createdAt)
	{
		InitializeComponent();
		_authService = authService;
		_mongoService = mongoService;
		_ffmpegService = ffmpegService;
		_projectId = projectId;
		_playlistId = playlistId;
		_fileName = fileName;
		_filePath = filePath;
		_sequencePosition = sequencePosition;
		
		// Set data
		FileTitleLabel.Text = fileName;
		CreatedAtLabel.Text = createdAt.ToString("dd/MM/yyyy, HH:mm:ss");
		SequencePositionLabel.Text = sequencePosition.ToString();
		SlotPositionEntry.Text = slotPosition;

		// Generate thumbnail
		_ = GenerateThumbnail();
	}

	private async Task GenerateThumbnail()
	{
		try
		{
			var ext = Path.GetExtension(_filePath).ToLower();
			var isVideo = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" }.Contains(ext);
			
			if (isVideo)
			{
				var thumbPath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.jpg");
				var generatedPath = await _ffmpegService.GenerateThumbnailAsync(_filePath, thumbPath, TimeSpan.FromSeconds(1));
				
				if (!string.IsNullOrEmpty(generatedPath) && File.Exists(generatedPath))
				{
					FileThumbnailImage.Source = ImageSource.FromFile(generatedPath);
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error generating thumbnail: {ex.Message}");
		}
	}
	
	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Navigation.PopAsync();
	}
	
	private async void OnPlayVideoClicked(object sender, EventArgs e)
	{
		try
		{
			Console.WriteLine($"[DEBUG] Attempting to play file: {_filePath}");
			Console.WriteLine($"[DEBUG] File exists: {File.Exists(_filePath)}");
			
			if (File.Exists(_filePath))
			{
				await Launcher.Default.OpenAsync(new OpenFileRequest
				{
					File = new ReadOnlyFile(_filePath)
				});
			}
			else
			{
				await NotificationService.ShowError($"File not found at: {_filePath}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[DEBUG] Error playing file: {ex.Message}");
			Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
			await NotificationService.ShowError($"Could not play file: {ex.Message}");
		}
	}
	
	private async void OnNotesClicked(object sender, EventArgs e)
	{
		try
		{
			// Get current notes from database
			var playlists = await _mongoService.GetProjectPlaylistsAsync(_projectId);
			var playlist = playlists.FirstOrDefault(p => p.Id == _playlistId);
			
			var notes = playlist?.Notes ?? new List<string>();
			
			var notesDialog = new NotesDialog(_mongoService, _playlistId, notes);
			await Navigation.PushModalAsync(notesDialog);
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to open notes: {ex.Message}");
		}
	}
	
	private async void OnSaveSlotClicked(object sender, EventArgs e)
	{
		try
		{
			var slot = SlotPositionEntry.Text?.Trim().ToLower() ?? "";
			
			if (string.IsNullOrEmpty(slot) || slot.Length != 1 || slot[0] < 'a' || slot[0] > 'z')
			{
				await NotificationService.ShowError("Slot must be a single letter from a to z");
				return;
			}
			
			// Check if slot is already used by another file in this project
			var allPlaylists = await _mongoService.GetProjectPlaylistsAsync(_projectId);
			var duplicateSlot = allPlaylists.FirstOrDefault(p => 
				p.Id != _playlistId && 
				p.SlotPosition?.ToLower() == slot);
			
			if (duplicateSlot != null)
			{
				await NotificationService.ShowError($"Slot '{slot.ToUpper()}' is already used by file '{duplicateSlot.FileName}'. Please choose a different slot.");
				return;
			}
			
			await _mongoService.UpdatePlaylistSlotAsync(_playlistId, slot);
			await NotificationService.ShowSuccess("Slot position saved successfully!");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to save slot: {ex.Message}");
		}
	}
	
	private async void OnDownloadFileClicked(object sender, EventArgs e)
	{
		try
		{
			if (!File.Exists(_filePath))
			{
				await NotificationService.ShowError("File not found.");
				return;
			}

			string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			string downloadsFolder = Path.Combine(userProfile, "Downloads", "Edit Farmer");
			
			// Ensure Downloads folder exists
			if (!Directory.Exists(downloadsFolder))
			{
				Directory.CreateDirectory(downloadsFolder);
			}

			string fullPath = Path.Combine(downloadsFolder, _fileName);
			string fileNameWithoutExt = Path.GetFileNameWithoutExtension(_fileName);
			string extension = Path.GetExtension(_fileName);
			int count = 1;

			while (File.Exists(fullPath))
			{
				string newName = $"{fileNameWithoutExt}({count}){extension}";
				fullPath = Path.Combine(downloadsFolder, newName);
				count++;
			}
			
			if (File.Exists(_filePath))
			{
				File.Copy(_filePath, fullPath);
				await NotificationService.ShowSuccess("File downloaded successfully!");
			}
			else
			{
				await NotificationService.ShowError("Source file not found");
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to download file: {ex.Message}");
		}
	}
	
	private async void OnDeleteFileClicked(object sender, EventArgs e)
	{
		var dialog = new ConfirmationDialog("Confirm Delete", $"Are you sure you want to delete '{_fileName}'?", "Delete", "Cancel");
		await Navigation.PushModalAsync(dialog);
		if (!await dialog.GetResultAsync()) return;
		
		try
		{
			// Delete from database
			await _mongoService.DeletePlaylistByIdAsync(_playlistId);
			
			// Delete physical file
			if (File.Exists(_filePath))
			{
				File.Delete(_filePath);
			}
			
			await NotificationService.ShowSuccess("File deleted successfully");
			await Navigation.PopAsync();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to delete file: {ex.Message}");
		}
	}
}

using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Database;

using CarrotDownload.Maui.Services;
using System.Linq;

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

		// Set up video preview card
		VideoPreviewCard.FilePath = filePath;
		VideoPreviewCard.FileName = fileName;
		
		// Get file size
		if (File.Exists(filePath))
		{
			var fileInfo = new FileInfo(filePath);
			string fileSizeText = fileInfo.Length < 1024 * 1024 
				? $"{fileInfo.Length / 1024.0:F1} KB" 
				: $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB";
			VideoPreviewCard.FileSize = fileSizeText;
		}
	}

	
	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Navigation.PopAsync();
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
			await NotificationService.ShowError("We couldn't open the notes. Please try again.");
		}
	}
	
	private async void OnSaveSlotClicked(object sender, EventArgs e)
	{
		try
		{
			var slotOriginal = SlotPositionEntry.Text?.Trim() ?? "";
			// Check for uppercase letters
			if (!string.IsNullOrEmpty(slotOriginal) && slotOriginal.Any(char.IsUpper))
			{
				await NotificationService.ShowError("Please use lowercase letters (a-z) for slots. Capital letters aren't allowed.");
				return;
			}
			var slot = slotOriginal.ToLower();
			
			if (string.IsNullOrEmpty(slot) || slot.Length != 1 || slot[0] < 'a' || slot[0] > 'z')
			{
				await NotificationService.ShowError("Slots should be a single letter from a to z.");
				return;
			}
			
			// Check if slot is already used by another file in this project
			var allPlaylists = await _mongoService.GetProjectPlaylistsAsync(_projectId);
			var duplicateSlot = allPlaylists.FirstOrDefault(p => 
				p.Id != _playlistId && 
				p.SlotPosition?.ToLower() == slot);
			
			if (duplicateSlot != null)
			{
				await NotificationService.ShowError($"Slot '{slot.ToUpper()}' is already taken by '{duplicateSlot.FileName}'. Please choose a different one.");
				return;
			}
			
			await _mongoService.UpdatePlaylistSlotAsync(_playlistId, slot);
			await NotificationService.ShowSuccess("Your slot position has been saved successfully!");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't save the slot position. Please try again.");
		}
	}
	
	private async void OnDownloadFileClicked(object sender, EventArgs e)
	{
		try
		{
			if (!File.Exists(_filePath))
			{
				await NotificationService.ShowError("We couldn't find that file.");
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
				await NotificationService.ShowSuccess("Success! Your file has been downloaded.");
			}
			else
			{
				await NotificationService.ShowError("We couldn't find the source file.");
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't download that file. Please try again.");
		}
	}
	
	private async void OnDeleteFileClicked(object sender, EventArgs e)
	{
		var dialog = new ConfirmationDialog("Confirm Delete", $"Are you sure you want to delete '{_fileName}'?", "Delete", "Cancel");
		await Navigation.PushModalAsync(dialog);
		if (!await dialog.GetResultAsync()) return;
		
		try
		{
			// CRITICAL: Delete from database ONLY - NEVER delete user's original files from disk
			// Files are stored at their original locations and must remain untouched
			await _mongoService.DeletePlaylistByIdAsync(_playlistId);
			
			await NotificationService.ShowSuccess("The file has been removed from the playlist successfully!");
			await Navigation.PopAsync();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't remove that file. Please try again.");
		}
	}
}

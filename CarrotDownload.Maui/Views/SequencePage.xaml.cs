using Microsoft.Maui.Controls;
using CarrotDownload.Database;
using CarrotDownload.Auth.Interfaces;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using CarrotDownload.Database.Models;
using CarrotDownload.Maui.Services;
using CarrotDownload.Maui.Helpers;

namespace CarrotDownload.Maui.Views;

public partial class SequencePage : ContentPage
{
	private readonly CarrotMongoService _mongoService;
	private readonly IAuthService _authService;
	private readonly CarrotDownload.FFmpeg.Interfaces.IFFmpegService _ffmpegService;
	private List<PlaylistCheckboxItem> _playlistItems = new();
	
	// Static processing queue to track across all instances
	private static List<string> _processingQueue = new();
	private static SemaphoreSlim _processingSemaphore = new SemaphoreSlim(1, 1);
	private static bool _isProcessing = false;
	
	public SequencePage(CarrotMongoService mongoService, IAuthService authService, CarrotDownload.FFmpeg.Interfaces.IFFmpegService ffmpegService)
	{
		InitializeComponent();
		_mongoService = mongoService;
		_authService = authService;
		_ffmpegService = ffmpegService;
	}
	
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		
		// TEST: Create a file to confirm this code is running
		try
		{
			var testPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "sequence_page_loaded.txt");
			File.WriteAllText(testPath, $"Sequence page loaded at {DateTime.Now}");
		}
		catch { }
		
		await LoadPlaylists();
		UpdateProcessingQueueDisplay();
	}
	
	private async Task LoadPlaylists()
	{
		try
		{
			// Get current user
			var currentUser = await _authService.GetCurrentUserAsync();
			if (currentUser == null) return;
			
			// Get all user projects (playlists)
			var projects = await _mongoService.GetUserProjectsAsync(currentUser.Id);
			
			PlaylistsContainer.Children.Clear();
			_playlistItems.Clear();
			
			foreach (var project in projects)
			{
				var checkboxItem = new PlaylistCheckboxItem
				{
					// Use the custom ProjectId (e.g. "b69636aa") which links to the playlists, 
					// not the Mongo ObjectId
					ProjectId = !string.IsNullOrEmpty(project.ProjectId) ? project.ProjectId : project.Id,
					ProjectTitle = project.Title,
					IsChecked = false,
					Project = project
				};
				
				// Check if this playlist is currently being processed
				bool isProcessing = _processingQueue.Contains(checkboxItem.ProjectId);
				
				// Create checkbox UI
				var checkboxContainer = new HorizontalStackLayout
				{
					Spacing = 10,
					Padding = new Thickness(0, 5)
				};
				
				var checkbox = new CheckBox
				{
					IsChecked = false,
					IsEnabled = !isProcessing, // Disable if processing
					Color = Color.FromArgb("#ff5722") // Use theme color
				};
				
				checkbox.CheckedChanged += (s, e) =>
				{
					checkboxItem.IsChecked = e.Value;
					UpdateProcessButtonText();
					UpdateCreateFileButton();
				};
				
				var label = new Label
				{
					Text = isProcessing ? $"{project.Title} (Processing...)" : project.Title,
					FontSize = 15,
					TextColor = isProcessing ? Color.FromArgb("#999") : Color.FromArgb("#333"),
					FontAttributes = isProcessing ? FontAttributes.Italic : FontAttributes.None,
					VerticalOptions = LayoutOptions.Center
				};
				
				checkboxContainer.Children.Add(checkbox);
				checkboxContainer.Children.Add(label);
				
				PlaylistsContainer.Children.Add(checkboxContainer);
				_playlistItems.Add(checkboxItem);
			}
			
			UpdateProcessButtonText();
			UpdateCreateFileButton();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to load playlists: {ex.Message}");
		}
	}
	
	private void UpdateProcessButtonText()
	{
		int selectedCount = _playlistItems.Count(p => p.IsChecked);
		ProcessButton.Text = $"Process {selectedCount} Playlist{(selectedCount != 1 ? "s" : "")}";
		
		// Enable/disable button based on selection
		if (selectedCount > 0)
		{
			ProcessButton.BackgroundColor = Color.FromArgb("#ff5722");
			ProcessButton.Opacity = 1.0;
			ProcessButton.TextColor = Colors.White;
			ProcessButton.IsEnabled = true;
		}
		else
		{
			ProcessButton.BackgroundColor = Color.FromArgb("#e0e0e0");
			ProcessButton.Opacity = 0.5;
			ProcessButton.TextColor = Color.FromArgb("#999999");
			ProcessButton.IsEnabled = false;
		}
	}
	
	private void UpdateCreateFileButton()
	{
		int selectedCount = _playlistItems.Count(p => p.IsChecked);
		
		// Enable/disable Create File button based on selection
		if (selectedCount > 0)
		{
			CreateFileButton.BackgroundColor = Color.FromArgb("#ff5722");
			CreateFileButton.Opacity = 1.0;
			CreateFileButton.TextColor = Colors.White;
			CreateFileButton.IsEnabled = true;
		}
		else
		{
			CreateFileButton.BackgroundColor = Color.FromArgb("#e0e0e0");
			CreateFileButton.Opacity = 0.5;
			CreateFileButton.TextColor = Color.FromArgb("#999999");
			CreateFileButton.IsEnabled = false;
		}
	}
	
	private void UpdateProcessingQueueDisplay()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			ProcessingQueueList.Children.Clear();
			
			if (_processingQueue.Count == 0)
			{
				ProcessingQueueBorder.IsVisible = false;
				return;
			}
			
			ProcessingQueueBorder.IsVisible = true;
			
			foreach (var projectId in _processingQueue)
			{
				// Find the project title from our loaded playlists
				var playlistItem = _playlistItems.FirstOrDefault(p => p.ProjectId == projectId);
				string displayText = playlistItem != null ? playlistItem.ProjectTitle : projectId;
				
				var itemLabel = new Label
				{
					Text = $"• {displayText}",
					FontSize = 14,
					TextColor = Color.FromArgb("#856404"),
					Margin = new Thickness(10, 0, 0, 0)
				};
				
				ProcessingQueueList.Children.Add(itemLabel);
			}
		});
	}
	
	private async void OnCreateFileClicked(object sender, EventArgs e)
	{
		var selectedPlaylists = _playlistItems.Where(p => p.IsChecked).ToList();
		
		// Don't allow if no playlists selected
		if (!selectedPlaylists.Any()) return;
		
		// Check if any selected playlist is already being processed
		var alreadyProcessing = selectedPlaylists.Where(p => _processingQueue.Contains(p.ProjectId)).ToList();
		if (alreadyProcessing.Any())
		{
			string playlistNames = string.Join(", ", alreadyProcessing.Select(p => p.ProjectTitle));
			await NotificationService.ShowWarning($"The following playlist(s) are already being processed:\n{playlistNames}");
			return;
		}
		
		// Check if another process is currently running
		if (_isProcessing)
		{
			await NotificationService.ShowWarning("Another playlist is currently being processed. Please wait for it to complete.");
			return;
		}
		
		// Prepare preview of files to be rendered
		var previewMessage = new System.Text.StringBuilder();
		previewMessage.AppendLine("The following files will be merged into a SINGLE video in this order:\n");
		
		// Master list of all files to render in order
		var allFilesToRender = new List<string>();

		foreach (var item in selectedPlaylists)
		{
			previewMessage.AppendLine($"--- {item.ProjectTitle} ---");
			
			// Get playlist files
			var playlistItems = await _mongoService.GetProjectPlaylistsAsync(item.ProjectId);
			List<string> projectFiles = new List<string>();

			if (playlistItems.Any())
			{
				// Sort by OrderIndex strictly. Then by CreatedAt.
				var sortedItems = playlistItems.OrderBy(p => p.OrderIndex).ThenBy(p => p.CreatedAt).ToList();
				projectFiles = sortedItems.Select(p => p.FilePath).Where(File.Exists).ToList();
				
				for(int i=0; i<projectFiles.Count; i++)
				{
					previewMessage.AppendLine($"{i+1}. {Path.GetFileName(projectFiles[i])}");
				}
			}
			else
			{
				// ERROR: No files in DB.
				previewMessage.AppendLine($"(Error: No database records found for Project ID: {item.ProjectId})");
			}
			
			if (!projectFiles.Any())
			{
				previewMessage.AppendLine("(No files found)");
			}
			else
			{
				allFilesToRender.AddRange(projectFiles);
			}
			
			previewMessage.AppendLine();
		}

		if (!allFilesToRender.Any())
		{
			await NotificationService.ShowWarning("No valid files found in selected playlists.");
			return;
		}

		// Confirm render with DETAILED list
		var dialog = new ConfirmationDialog("Confirm Merged Video", 
			previewMessage.ToString(), 
			"Start Render", "Cancel");
		await Navigation.PushModalAsync(dialog);
			
		if (!await dialog.GetResultAsync()) return;

		// Mark as processing and add to queue
		_isProcessing = true;
		foreach (var playlist in selectedPlaylists)
		{
			_processingQueue.Add(playlist.ProjectId);
		}
		UpdateProcessingQueueDisplay();
		await LoadPlaylists(); // Refresh to show disabled checkboxes

		// Show loading indicator
		SuccessMessageLabel.Text = "Rendering merged video... Please wait.";
		SuccessMessageBorder.IsVisible = true;
		
		try
		{
			// Save to user's Downloads folder
			string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			if (!Directory.Exists(downloadsPath))
			{
				Directory.CreateDirectory(downloadsPath);
			}
			
			// Determine output filename
			string outputFileName;
			if (selectedPlaylists.Count == 1)
			{
				outputFileName = $"{selectedPlaylists[0].ProjectTitle}_Final_{DateTime.Now:yyyyMMdd_HHmm}.mp4";
			}
			else
			{
				outputFileName = $"Merged_Playlists_{DateTime.Now:yyyyMMdd_HHmm}.mp4";
			}
			
				// Sanitize filename
				outputFileName = string.Join("_", outputFileName.Split(Path.GetInvalidFileNameChars()));
				string outputFilePath = Path.Combine(downloadsPath, outputFileName);
				
				// Ensure unique filename
				int counter = 1;
				string fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputFileName);
				string ext = Path.GetExtension(outputFileName);
				
				while (File.Exists(outputFilePath))
				{
					string newName = $"{fileNameWithoutExt}_{counter}{ext}";
					outputFilePath = Path.Combine(downloadsPath, newName);
					outputFileName = newName; // Update for history
					counter++;
				}

			SuccessMessageLabel.Text = $"Merging {allFilesToRender.Count} files into '{outputFileName}'...";

			// Run concatenation ONCE for all files
			var result = await _ffmpegService.ConcatenateMediaAsync(allFilesToRender, outputFilePath);

			// Final status
			SuccessMessageBorder.IsVisible = false;

			if (result.Success)
			{
				// Save to Export History so it appears in Downloads tab
				var currentUser = await _authService.GetCurrentUserAsync();
				if (currentUser != null)
				{
					var exportHistory = new CarrotDownload.Database.Models.ExportHistoryModel
					{
						UserId = currentUser.Id,
						ZipFileName = outputFileName, // Storing MP4 name here
						ZipFilePath = outputFilePath, // Storing MP4 path here
						ProjectTitles = selectedPlaylists.Select(p => p.ProjectTitle).ToList(),
						TotalFiles = 1,
						ExportedAt = DateTime.UtcNow
					};
					await _mongoService.CreateExportHistoryAsync(exportHistory);
				}

				// Playlists and file addresses are preserved - no cleanup performed

				await NotificationService.ShowSuccess($"Video rendering completed!\nSaved to: {outputFilePath}\nCheck the Downloads tab.");
			}
			else
			{
				await NotificationService.ShowError($"Rendering failed: {result.ErrorMessage}");
			}
		}
		catch (Exception ex)
		{
			SuccessMessageBorder.IsVisible = false;
			await NotificationService.ShowError($"Rendering failed: {ex.Message}");
		}
		finally
		{
			// Remove from processing queue and update UI
			foreach (var playlist in selectedPlaylists)
			{
				_processingQueue.Remove(playlist.ProjectId);
			}
			_isProcessing = false;
			UpdateProcessingQueueDisplay();
			await LoadPlaylists(); // Refresh to re-enable checkboxes
		}
	}
	
	private async void OnProcessPlaylistsClicked(object sender, EventArgs e)
	{
		var selectedPlaylists = _playlistItems.Where(p => p.IsChecked).ToList();
		
		// Don't allow if no playlists selected
		if (!selectedPlaylists.Any()) return;
		
		// 1. PREPARE DATA & SHOW CONFIRMATION
		var previewMessage = new System.Text.StringBuilder();
		previewMessage.AppendLine("The following files will be included in the ZIP export:\n");
		
		// Map: ProjectId -> List of file paths
		var preparedData = new Dictionary<string, List<string>>();

		foreach (var item in selectedPlaylists)
		{
			previewMessage.AppendLine($"--- {item.ProjectTitle} ---");
			
			// Get playlist files
			var playlistItems = await _mongoService.GetProjectPlaylistsAsync(item.ProjectId);
			List<string> projectFiles = new List<string>();

			if (playlistItems.Any())
			{
				// Sort by OrderIndex strictly. Then by CreatedAt.
				var sortedItems = playlistItems.OrderBy(p => p.OrderIndex).ThenBy(p => p.CreatedAt).ToList();
				projectFiles = sortedItems.Select(p => p.FilePath).Where(File.Exists).ToList();
				
				for(int i=0; i<projectFiles.Count; i++)
				{
					previewMessage.AppendLine($"{i+1}. {Path.GetFileName(projectFiles[i])}");
				}
			}
			else
			{
				// ERROR: No files in DB.
				previewMessage.AppendLine($"(Error: No database records found for Project ID: {item.ProjectId})");
			}
			
			if (!projectFiles.Any())
			{
				previewMessage.AppendLine("(No files found)");
			}
			
			previewMessage.AppendLine();
			preparedData[item.ProjectId] = projectFiles;
		}

		// check if we have any files at all
		bool hasAnyFiles = preparedData.Values.Any(l => l.Any());
		if (!hasAnyFiles)
		{
			await NotificationService.ShowWarning("No valid files found to export.");
			return;
		}

		// 2. SHOW CONFIRMATION DIALOG
		var dialog = new ConfirmationDialog("Confirm ZIP Export", 
			previewMessage.ToString(), 
			"Start Export", "Cancel");
		await Navigation.PushModalAsync(dialog);
			
		bool confirmed = await dialog.GetResultAsync();
		if (!confirmed) return;
		
		// 3. START PROCESSING
		SuccessMessageLabel.Text = $"Exporting {selectedPlaylists.Count} playlists...";
		SuccessMessageBorder.IsVisible = true;
		
		try 
		{
			string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			string downloadsPath = Path.Combine(userProfile, "Downloads");
			
			string zipFileName = selectedPlaylists.Count == 1 
				? $"{selectedPlaylists[0].ProjectTitle}_Export_{DateTime.Now:yyyyMMdd_HHmm}.zip"
				: $"Merged_Playlists_Export_{DateTime.Now:yyyyMMdd_HHmm}.zip";
			
			// Sanitize filename
			zipFileName = string.Join("_", zipFileName.Split(Path.GetInvalidFileNameChars()));
			string zipFilePath = Path.Combine(downloadsPath, zipFileName);
			
			// Ensure unique filename for ZIP
			int counter = 1;
			string fileNameWithoutExt = Path.GetFileNameWithoutExtension(zipFileName);
			string ext = Path.GetExtension(zipFileName);
			while (File.Exists(zipFilePath))
			{
				string newName = $"{fileNameWithoutExt}_{counter}{ext}";
				zipFilePath = Path.Combine(downloadsPath, newName);
				zipFileName = newName;
				counter++;
			}
			
			// Create temp directory for organizing
			string tempRoot = Path.Combine(FileSystem.CacheDirectory, $"Temp_Zip_{DateTime.Now.Ticks}");
			Directory.CreateDirectory(tempRoot);
			
			int totalFilesCopied = 0;

			foreach (var item in selectedPlaylists)
			{
				if (!preparedData.ContainsKey(item.ProjectId)) continue;
				var filesToExport = preparedData[item.ProjectId];
				
				if (!filesToExport.Any()) continue;

				// Create folder for playlist inside ZIP
				string sanitizedName = string.Join("_", item.ProjectTitle.Split(Path.GetInvalidFileNameChars()));
				string playlistDir = Path.Combine(tempRoot, sanitizedName);
				Directory.CreateDirectory(playlistDir);

				// Iterate with index to add sequence number (1 based)
				for (int i = 0; i < filesToExport.Count; i++)
				{
					var filePath = filesToExport[i];
					string originalFileName = Path.GetFileName(filePath);
					// Format: 1_FileName.mp3 (Preserve sequence in filename)
					string sequencedFileName = $"{i + 1}_{originalFileName}";
					
					string destPath = Path.Combine(playlistDir, sequencedFileName);
					File.Copy(filePath, destPath, overwrite: true);
					totalFilesCopied++;
				}
			}

			// Create Zip if files exist
			if (totalFilesCopied > 0)
			{
				ZipFile.CreateFromDirectory(tempRoot, zipFilePath);

				SuccessMessageBorder.IsVisible = false;
				await NotificationService.ShowSuccess($"Export completed!\nSaved to: {zipFilePath}\nCheck Downloads folder.");
			}
			else
			{
				SuccessMessageBorder.IsVisible = false;
				await NotificationService.ShowWarning("No files could be copied for export.");
			}

			// Cleanup Temp
			try { Directory.Delete(tempRoot, true); } catch { }
		}
		catch (Exception ex)
		{
			SuccessMessageBorder.IsVisible = false;
			await NotificationService.ShowError($"Export failed: {ex.Message}");
		}
	}

	
	private class PlaylistCheckboxItem
	{
		public string ProjectId { get; set; }
		public string ProjectTitle { get; set; }
		public bool IsChecked { get; set; }
		public CarrotDownload.Database.Models.ProjectModel Project { get; set; }
	}
	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//DashboardPage");
	}
}

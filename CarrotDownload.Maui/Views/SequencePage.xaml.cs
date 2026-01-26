using Microsoft.Maui.Controls;
using CarrotDownload.Database;
using CarrotDownload.Auth.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using CarrotDownload.Database.Models;
using CarrotDownload.Maui.Services;
using CarrotDownload.Maui.Helpers;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
#if WINDOWS
using WinRT.Interop;
using Microsoft.UI.Xaml;
#endif

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
					Padding = new Microsoft.Maui.Thickness(0, 5)
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
			await NotificationService.ShowError("We couldn't load your playlists. Please try again.");
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
					Margin = new Microsoft.Maui.Thickness(10, 0, 0, 0)
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
			await NotificationService.ShowWarning($"These playlists are already being processed:\n{playlistNames}");
			return;
		}
		
		// Check if another process is currently running
		if (_isProcessing)
		{
			await NotificationService.ShowWarning("Another playlist is currently processing. Please wait for it to finish.");
			return;
		}
		
		// Prepare preview of files to be rendered - ONE VIDEO PER PLAYLIST
		var previewMessage = new System.Text.StringBuilder();
		previewMessage.AppendLine($"The following {selectedPlaylists.Count} playlist(s) will each be rendered as separate videos:\n");
		
		// Map each playlist to its files
		var playlistFilesMap = new Dictionary<string, List<string>>();

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
				playlistFilesMap[item.ProjectId] = projectFiles;
			}
			
			previewMessage.AppendLine();
		}

		if (!playlistFilesMap.Any() || !playlistFilesMap.Values.Any(l => l.Any()))
		{
			await NotificationService.ShowWarning("We couldn't find any files in those playlists. Please add some files first.");
			return;
		}

		// Confirm render with DETAILED list
		var dialog = new ConfirmationDialog("Confirm Video Rendering", 
			previewMessage.ToString(), 
			"Start Render", "Cancel");
		await Navigation.PushModalAsync(dialog);
			
		if (!await dialog.GetResultAsync()) return;

		// Let the user choose where to save the rendered videos
		string selectedFolderPath = null;

#if WINDOWS
		try
		{
			var folderPicker = new Windows.Storage.Pickers.FolderPicker();
			folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
			folderPicker.FileTypeFilter.Add("*");
			
			// Initialize with window handle (required for desktop apps)
			var window = App.Current.Windows.FirstOrDefault();
			if (window != null && window.Handler?.PlatformView is Microsoft.UI.Xaml.Window platformWindow)
			{
				var windowHandle = GetWindowHandle(platformWindow);
				InitializeWithWindow.Initialize(folderPicker, windowHandle);
			}
			
			var folder = await folderPicker.PickSingleFolderAsync();
			
			if (folder != null)
			{
				selectedFolderPath = folder.Path;
			}
			else
			{
				await NotificationService.ShowInfo("Rendering cancelled. Please select a folder to continue.");
				return;
			}
		}
		catch (Exception folderPickerEx)
		{
			await NotificationService.ShowError("We couldn't open the folder picker. Please try again.");
			return;
		}
#else
		// Fallback for non-Windows platforms - use Downloads folder
		string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		if (!Directory.Exists(downloadsPath))
		{
			Directory.CreateDirectory(downloadsPath);
		}
		selectedFolderPath = downloadsPath;
#endif

		// Mark as processing and add to queue
		_isProcessing = true;
		foreach (var playlist in selectedPlaylists)
		{
			_processingQueue.Add(playlist.ProjectId);
		}
		UpdateProcessingQueueDisplay();
		await LoadPlaylists(); // Refresh to show disabled checkboxes

		// Show loading indicator
		SuccessMessageLabel.Text = $"Rendering {selectedPlaylists.Count} video(s)... Please wait.";
		SuccessMessageBorder.IsVisible = true;
		
		try
		{
			// Ensure selected folder exists
			if (!Directory.Exists(selectedFolderPath))
			{
				Directory.CreateDirectory(selectedFolderPath);
			}
			
			var currentUser = await _authService.GetCurrentUserAsync();
			int successCount = 0;
			int failCount = 0;

			// Render EACH playlist separately
			foreach (var playlistItem in selectedPlaylists)
			{
				if (!playlistFilesMap.ContainsKey(playlistItem.ProjectId) || !playlistFilesMap[playlistItem.ProjectId].Any())
				{
					await NotificationService.ShowWarning($"Skipping '{playlistItem.ProjectTitle}' - no files found to render.");
					continue;
			}

				var filesToRender = playlistFilesMap[playlistItem.ProjectId];
				
				// Generate unique filename for this playlist
				string outputFileName = $"{playlistItem.ProjectTitle}_Final_{DateTime.Now:yyyyMMdd_HHmm}.mp4";
				outputFileName = string.Join("_", outputFileName.Split(Path.GetInvalidFileNameChars()));
				string outputFilePath = Path.Combine(selectedFolderPath, outputFileName);
				
				// Ensure unique filename
				int counter = 1;
				string fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputFileName);
				string ext = Path.GetExtension(outputFileName);
				
				while (File.Exists(outputFilePath))
				{
					string newName = $"{fileNameWithoutExt}_{counter}{ext}";
					outputFilePath = Path.Combine(selectedFolderPath, newName);
					outputFileName = newName;
					counter++;
				}

				SuccessMessageLabel.Text = $"Rendering '{playlistItem.ProjectTitle}' ({filesToRender.Count} files)...";

				// Render this playlist's files
				var result = await _ffmpegService.ConcatenateMediaAsync(filesToRender, outputFilePath);

			if (result.Success)
			{
				// Save to Export History so it appears in Downloads tab
				if (currentUser != null)
				{
					var exportHistory = new CarrotDownload.Database.Models.ExportHistoryModel
					{
						UserId = currentUser.Id,
							ZipFileName = outputFileName,
							ZipFilePath = outputFilePath,
							ProjectTitles = new List<string> { playlistItem.ProjectTitle },
						TotalFiles = 1,
						ExportedAt = DateTime.UtcNow
					};
					await _mongoService.CreateExportHistoryAsync(exportHistory);
				}
					successCount++;
				}
				else
				{
					failCount++;
					await NotificationService.ShowError($"We couldn't render '{playlistItem.ProjectTitle}'. Please check the file and try again.");
				}
			}

			// Final status
			SuccessMessageBorder.IsVisible = false;

			if (successCount > 0)
			{
				string message = successCount == 1
					? $"Video rendering completed!\nSaved to:\n{selectedFolderPath}\n\nCheck the Downloads tab."
					: $"{successCount} video(s) rendered successfully!\nSaved to:\n{selectedFolderPath}\n\nCheck the Downloads tab.";
				if (failCount > 0)
				{
					message += $"\n{failCount} video(s) failed to render.";
				}
				await NotificationService.ShowSuccess(message);
			}
			else
			{
				await NotificationService.ShowError("Unfortunately, all video renders failed. Please check your files and try again.");
			}
		}
		catch (Exception ex)
		{
			SuccessMessageBorder.IsVisible = false;
			await NotificationService.ShowError("The rendering process encountered an error. Please try again.");
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
			await NotificationService.ShowWarning("We couldn't find any files to export. Please add some files first.");
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
			// Build a suggested ZIP file name (no path yet)
			string zipFileName = selectedPlaylists.Count == 1 
				? $"{selectedPlaylists[0].ProjectTitle}_Export_{DateTime.Now:yyyyMMdd_HHmm}.zip"
				: $"Merged_Playlists_Export_{DateTime.Now:yyyyMMdd_HHmm}.zip";
			
			// Sanitize filename
			zipFileName = string.Join("_", zipFileName.Split(Path.GetInvalidFileNameChars()));
			
			// Create temp directory for organizing files to zip
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

			if (totalFilesCopied == 0)
			{
				SuccessMessageBorder.IsVisible = false;
				await NotificationService.ShowWarning("We couldn't copy any files for export. Please check that the files exist.");
				// Cleanup Temp
				try { Directory.Delete(tempRoot, true); } catch { }
				return;
			}

			// Create ZIP in a temporary location
			string tempZipPath = Path.Combine(FileSystem.CacheDirectory, $"Temp_Export_{DateTime.Now.Ticks}.zip");
			ZipFile.CreateFromDirectory(tempRoot, tempZipPath);

			// Let the user choose where to save the ZIP
			string selectedFolderPath = null;

#if WINDOWS
			try
			{
				var folderPicker = new Windows.Storage.Pickers.FolderPicker();
				folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
				folderPicker.FileTypeFilter.Add("*");
				
				// Initialize with window handle (required for desktop apps)
				var window = App.Current.Windows.FirstOrDefault();
				if (window != null && window.Handler?.PlatformView is Microsoft.UI.Xaml.Window platformWindow)
			{
					var windowHandle = GetWindowHandle(platformWindow);
					InitializeWithWindow.Initialize(folderPicker, windowHandle);
				}
				
				var folder = await folderPicker.PickSingleFolderAsync();
				
				if (folder != null)
				{
					selectedFolderPath = folder.Path;
				}
				else
				{
					SuccessMessageBorder.IsVisible = false;
					await NotificationService.ShowInfo("Export cancelled. Please select a folder to continue.");
					// Cleanup Temp
					try 
					{ 
						if (File.Exists(tempZipPath))
						{
							File.Delete(tempZipPath);
						}
						if (Directory.Exists(tempRoot))
						{
							Directory.Delete(tempRoot, true);
						}
					} 
					catch { }
					return;
				}
			}
			catch (Exception folderPickerEx)
			{
				SuccessMessageBorder.IsVisible = false;
				await NotificationService.ShowError("We couldn't open the folder picker. Please try again.");
				// Cleanup Temp
				try 
				{ 
					if (File.Exists(tempZipPath))
					{
						File.Delete(tempZipPath);
					}
					if (Directory.Exists(tempRoot))
					{
						Directory.Delete(tempRoot, true);
					}
				} 
				catch { }
				return;
			}
#else
			// Fallback for non-Windows platforms
			await using (var zipStream = File.OpenRead(tempZipPath))
			{
				var result = await FileSaver.Default.SaveAsync(zipFileName, zipStream);
				
				if (result.IsSuccessful)
				{
					selectedFolderPath = Path.GetDirectoryName(result.FilePath);
			}
			else
			{
				SuccessMessageBorder.IsVisible = false;
					await NotificationService.ShowWarning("The export ZIP was created, but we couldn't save it. Please try again.");
					// Cleanup Temp
					try 
					{ 
						if (File.Exists(tempZipPath))
						{
							File.Delete(tempZipPath);
						}
						if (Directory.Exists(tempRoot))
						{
							Directory.Delete(tempRoot, true);
						}
					} 
					catch { }
					return;
				}
			}
#endif

			// Copy ZIP file to selected folder
			if (!string.IsNullOrEmpty(selectedFolderPath))
			{
				try
				{
					string destinationPath = Path.Combine(selectedFolderPath, zipFileName);
					
					// Handle duplicate filenames
					int counter = 1;
					string fileNameWithoutExt = Path.GetFileNameWithoutExtension(zipFileName);
					string ext = Path.GetExtension(zipFileName);
					while (File.Exists(destinationPath))
					{
						string newName = $"{fileNameWithoutExt}_{counter}{ext}";
						destinationPath = Path.Combine(selectedFolderPath, newName);
						counter++;
					}
					
					File.Copy(tempZipPath, destinationPath, overwrite: false);
					
					SuccessMessageBorder.IsVisible = false;
					await NotificationService.ShowSuccess($"Success! Your export is ready at:\n{destinationPath}");
				}
				catch (Exception copyEx)
				{
					SuccessMessageBorder.IsVisible = false;
					await NotificationService.ShowError("We couldn't save your ZIP file. Please try again.");
				}
			}

			// Cleanup Temp
			try 
			{ 
				if (File.Exists(tempZipPath))
				{
					File.Delete(tempZipPath);
				}
				if (Directory.Exists(tempRoot))
				{
					Directory.Delete(tempRoot, true);
				}
			} 
			catch { }
		}
		catch (Exception ex)
		{
			SuccessMessageBorder.IsVisible = false;
			await NotificationService.ShowError("The export process encountered an error. Please try again.");
		}
	}

	
#if WINDOWS
	// Helper method to get window handle from WinUI Window using P/Invoke
	[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr GetActiveWindow();

	private static IntPtr GetWindowHandle(Microsoft.UI.Xaml.Window window)
	{
		// Use the window's Content property to get the HWND
		// For WinUI 3, we need to use Microsoft.UI.Win32Interop
		var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(window.AppWindow.Id);
		return hwnd;
	}
#endif
	
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

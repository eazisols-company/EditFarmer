using CarrotDownload.Auth.Interfaces;
using System.Collections.ObjectModel;
using CarrotDownload.Maui.Services;
using System.Linq;
using Microsoft.Maui.Controls;
using CarrotDownload.Maui.Controls;

namespace CarrotDownload.Maui.Views;

public partial class ProjectDetailPage : ContentPage
{
	private readonly IAuthService _authService;
	private readonly CarrotDownload.Database.CarrotMongoService _mongoService;
	private readonly CarrotDownload.FFmpeg.Interfaces.IFFmpegService _ffmpegService;
	private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".mp3", ".wav", ".mp4", ".mkv", ".avi", ".mov", ".flac", ".aac"
	};
	private string _projectId;
	private string _projectTitle;


	public ObservableCollection<ProjectFileModel> ProjectFiles { get; set; } = new();
	public ObservableCollection<ProjectFileModel> PlaylistFiles { get; set; } = new(); // Temporary before upload
	public ObservableCollection<ProjectFileModel> FinalizedPlaylistFiles { get; set; } = new(); // From database
	public ObservableCollection<ProjectFileModel> TempFiles { get; set; } = new(); // Multiple files before upload
	
	private List<ProjectFileModel> _selectedFiles = new(); // Track selected files for Add to Playlist

	public ProjectDetailPage(IAuthService authService, 
                            CarrotDownload.Database.CarrotMongoService mongoService,
                            CarrotDownload.FFmpeg.Interfaces.IFFmpegService ffmpegService)
	{
		InitializeComponent();
		_authService = authService;
		_mongoService = mongoService;
		_ffmpegService = ffmpegService;
		BindingContext = this;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		
		if (!string.IsNullOrEmpty(_projectId))
		{
			// Refresh data when page appears (e.g. returning from detail page)
			await LoadProjectFiles();
			await LoadPlaylistsFromDatabase();
		}
	}

	public void LoadProject(string projectId, string projectTitle)
	{
		_projectId = projectId;
		_projectTitle = projectTitle;

		ProjectTitleLabel.Text = $"Project: {projectTitle}";

		// Data loading is now handled in OnAppearing to ensure refresh on return
	}

	private async Task LoadPlaylistsFromDatabase()
	{
		try
		{
			FinalizedPlaylistFiles.Clear();

			var playlists = await _mongoService.GetProjectPlaylistsAsync(_projectId);
			
			// Sort by OrderIndex strictly, then by CreatedAt to ensure stable sort matching DB/SequencePage
			var sortedPlaylists = playlists.OrderBy(p => p.OrderIndex).ThenBy(p => p.CreatedAt).ToList();
			
			// AUTO-FIX: Check if OrderIndex values need to be renumbered to match visual order (1-based)
			bool needsRenumbering = false;
			for (int i = 0; i < sortedPlaylists.Count; i++)
			{
				int expectedOrderIndex = i + 1; // 1-based: 1, 2, 3...
				if (sortedPlaylists[i].OrderIndex != expectedOrderIndex)
				{
					needsRenumbering = true;
					break;
				}
			}
			
			// If OrderIndex is out of sync, renumber them to 1-based
			if (needsRenumbering)
			{
				System.Diagnostics.Debug.WriteLine("[AUTO-FIX] Renumbering OrderIndex to 1-based to match visual order");
				for (int i = 0; i < sortedPlaylists.Count; i++)
				{
					var playlist = sortedPlaylists[i];
					int newOrderIndex = i + 1; // 1-based
					if (playlist.OrderIndex != newOrderIndex)
					{
						System.Diagnostics.Debug.WriteLine($"  Updating {playlist.FileName}: OrderIndex {playlist.OrderIndex} -> {newOrderIndex}");
						await _mongoService.UpdatePlaylistSlotAndOrderAsync(playlist.Id, playlist.SlotPosition, newOrderIndex);
						playlist.OrderIndex = newOrderIndex; // Update local copy
					}
				}
			}
			
			int index = 1;
			foreach (var playlist in sortedPlaylists)
			{
				// Convert slot letter to index (a=0, b=1, etc.) for internal model if needed, 
				// but we'll use Index for display.
				int slotIndex = !string.IsNullOrEmpty(playlist.SlotPosition) && playlist.SlotPosition.Length > 0
					? playlist.SlotPosition.ToLower()[0] - 'a'
					: -1;

				var newFile = new ProjectFileModel
				{
					Index = index++,
					FileName = playlist.FileName,
					FilePath = playlist.FilePath,
					SlotPosition = slotIndex,
					SlotLetter = playlist.SlotPosition?.ToLower() ?? ""
				};
				FinalizedPlaylistFiles.Add(newFile);
				
				// Generate thumbnail asynchronously
				_ = Task.Run(async () => {
					var thumb = await GenerateThumbnailForFile(newFile.FilePath);
					if (thumb != null) MainThread.BeginInvokeOnMainThread(() => newFile.Thumbnail = thumb);
				});
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't load your playlists. Please try again.");
		}
	}

	// Load project files from database
	private async Task LoadProjectFiles()
	{
		try
		{
			ProjectFiles.Clear();

			// Get project from database to retrieve the file list
			var project = await _mongoService.GetProjectByIdAsync(_projectId);
			if (project != null && project.Files != null && project.Files.Any())
			{
				int index = 1;
				foreach (var filePath in project.Files)
				{
					var newFile = new ProjectFileModel
					{
						Index = index++,
						FileName = Path.GetFileName(filePath),
						FilePath = filePath
					};
					ProjectFiles.Add(newFile);

					// Generate thumbnail asynchronously
					_ = Task.Run(async () => {
						var thumb = await GenerateThumbnailForFile(newFile.FilePath);
						if (thumb != null) MainThread.BeginInvokeOnMainThread(() => newFile.Thumbnail = thumb);
					});
				}
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't load your project files. Please try again.");
		}
	}

	private async void OnPickFileClicked(object sender, EventArgs e)
	{
		try
		{
			var options = new PickOptions
			{
				PickerTitle = "Select Audio or Video Files",
				FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
				{
					{ DevicePlatform.iOS, new[] { "public.audio", "public.movie" } },
					{ DevicePlatform.Android, new[] { "audio/*", "video/*" } },
					{ DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".mp4", ".mkv", ".avi", ".mov", ".flac", ".aac" } }
				})
			};

			var results = await FilePicker.Default.PickMultipleAsync(options);
			if (results != null && results.Any())
			{
				foreach (var result in results)
				{
					await AddToTempFiles(result.FullPath);
				}
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't select those files. Please try again.");
		}
	}

	private async Task AddToTempFiles(string filePath)
	{
		if (TempFiles.Any(f => f.FilePath == filePath)) return;

		var newTempFile = new ProjectFileModel
		{
			FileName = Path.GetFileName(filePath),
			FilePath = filePath
		};

		TempFiles.Add(newTempFile);
		TempFilesCollectionView.IsVisible = true;
		UploadFileButton.IsEnabled = true;

		// Generate thumbnail
		var thumb = await GenerateThumbnailForFile(filePath);
		if (thumb != null)
		{
			newTempFile.Thumbnail = thumb;
		}
	}

	private void OnDragOver(object sender, DragEventArgs e)
	{
		e.AcceptedOperation = DataPackageOperation.Copy;

#if WINDOWS
        if (e.PlatformArgs?.DragEventArgs != null)
        {
            e.PlatformArgs.DragEventArgs.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.PlatformArgs.DragEventArgs.DragUIOverride.IsCaptionVisible = true;
            e.PlatformArgs.DragEventArgs.DragUIOverride.Caption = "Drop to upload files";
        }
#endif

		if (sender is Border border)
		{
			border.Stroke = Color.FromArgb("#2196F3");
			border.BackgroundColor = Color.FromArgb("#e3f2fd");
		}
	}

	private void OnDragLeave(object sender, DragEventArgs e)
	{
		if (sender is Border border)
		{
			border.Stroke = Color.FromArgb("#d0d0d0");
			border.BackgroundColor = Color.FromArgb("#fafafa");
		}
	}

	private async void OnFilesDropped(object sender, DropEventArgs e)
	{
		OnDragLeave(sender, null!);

		try
		{
			// Handle file drops if supported by platform
			// On Windows, the data package often contains file paths
			var paths = await GetPathsFromDrop(e);
			if (paths != null && paths.Any())
			{
				foreach (var path in paths)
				{
					await AddToTempFiles(path);
				}
			}
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
	
	private void OnRemoveTempFileClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectFileModel file)
		{
			TempFiles.Remove(file);
			if (TempFiles.Count == 0)
			{
				TempFilesCollectionView.IsVisible = false;
				UploadFileButton.IsEnabled = false;
			}
		}
	}

	private async void OnUploadFilesClicked(object sender, EventArgs e)
	{
		if (TempFiles.Count == 0)
		{
			await NotificationService.ShowInfo("Please select at least one file to continue.");
			return;
		}
		
		try
		{
			// Get current project to update its Files list
			var project = await _mongoService.GetProjectByIdAsync(_projectId);
			var projectFiles = project?.Files?.ToList() ?? new List<string>();

			int uploadCount = 0;
			foreach (var tempFile in TempFiles.ToList())
			{
				// Store original file path - no copying
				// Allow duplicates: every upload should create a visible entry.
				projectFiles.Add(tempFile.FilePath);
				uploadCount++;
				
				// Remove from temp list
				TempFiles.Remove(tempFile);
			}
			
			// Update project's Files list in database if changes were made
			if (uploadCount > 0)
			{
				await _mongoService.UpdateProjectFilesAsync(_projectId, projectFiles);
				await NotificationService.ShowSuccess($"Great! {uploadCount} file(s) have been added to your project.");
			}
			
			// Clear UI
			TempFilesCollectionView.IsVisible = false;
			UploadFileButton.IsEnabled = false;
			
			// Reload files to show new files
			await LoadProjectFiles();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't add those files. Please try again.");
		}
	}

	private async void OnDeleteFileClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectFileModel file)
		{
			try
			{
				var dialog = new ConfirmationDialog("Remove File", 
					$"Are you sure you want to remove '{file.FileName}' from the project?", 
					"Remove", "Cancel");
				await Navigation.PushModalAsync(dialog);
				
				if (!await dialog.GetResultAsync()) return;

				// CRITICAL: Do NOT delete physical file - just remove from project
				// Files are stored at their original locations and must remain untouched
				// Only update the project's Files list in the database
				var project = await _mongoService.GetProjectByIdAsync(_projectId);
				if (project != null && project.Files != null)
				{
					var updatedFiles = project.Files
						.Where(path => !string.Equals(path, file.FilePath, StringComparison.OrdinalIgnoreCase))
						.ToList();

					await _mongoService.UpdateProjectFilesAsync(_projectId, updatedFiles);
				}

				// Reload files
				LoadProjectFiles();

				await NotificationService.ShowSuccess($"'{file.FileName}' has been removed from your project.");
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError("We couldn't remove that file. Please try again.");
			}
		}
	}

	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("..");
	}

	private async void OnPlayVideoClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is string filePath)
		{
			try
			{
				await Launcher.Default.OpenAsync(new OpenFileRequest
				{
					File = new ReadOnlyFile(filePath)
				});
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError("We couldn't play that file. Please make sure it exists.");
			}
		}
	}
	
	// Navigate to file detail page when filename is clicked
	private async void OnFileNameClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectFileModel file)
		{
			try
			{
				// Get playlist data from database to get ID and created date
				var playlists = await _mongoService.GetProjectPlaylistsAsync(_projectId);
				var playlist = playlists.FirstOrDefault(p => p.FilePath == file.FilePath);
				
				if (playlist != null)
				{
					var detailPage = new PlaylistFileDetailPage(
						_authService,
						_mongoService,
						_ffmpegService,
						_projectId,
						playlist.Id,
						file.FileName,
						file.FilePath,
						file.Index,
						file.SlotLetter ?? "",
						playlist.CreatedAt
					);
					
					await Navigation.PushAsync(detailPage);
				}
				else
				{
					await NotificationService.ShowError("We couldn't find that playlist file. It may have been removed.");
				}
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError("We couldn't open those file details. Please try again.");
			}
		}
	}
	
	// File selection handler (double-click)
	private void OnFileDoubleClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectFileModel file)
		{
			// Toggle selection
			if (_selectedFiles.Contains(file))
			{
				// Deselect
				_selectedFiles.Remove(file);
				file.IsSelected = false;
			}
			else
			{
				// Select
				_selectedFiles.Add(file);
				file.IsSelected = true;
			}
			
			// Show/hide Add to Playlist button
			AddToPlaylistButton.IsVisible = _selectedFiles.Count > 0;
		}
	}
	
	// Add selected files to playlist without slots
	private async void OnAddToPlaylistClicked(object sender, EventArgs e)
	{
		if (!_selectedFiles.Any())
		{
			await NotificationService.ShowInfo("Please select at least one file to continue.");
			return;
		}
		
		try
		{
			// Get current project so we can reference its existing file list if needed.
			// NOTE: We no longer remove files from the project's library when adding
			// them to a playlist; project files should persist.
			var project = await _mongoService.GetProjectByIdAsync(_projectId);

			// Add selected files to permanent playlist (FinalizedPlaylistFiles) and save to database
			// Using original file paths - no file copying
			foreach (var file in _selectedFiles)
			{
				var fileName = Path.GetFileName(file.FilePath);
				
				// Allow duplicates: adding the same file again should create another playlist entry.
				// Use Index starting from 1. Slot is left empty by default.
				var playlistFile = new ProjectFileModel
				{
					Index = FinalizedPlaylistFiles.Count + 1,
					FileName = fileName,
					FilePath = file.FilePath, // Use original file path
					SlotPosition = -1,
					SlotLetter = "" 
				};
				
				FinalizedPlaylistFiles.Add(playlistFile);

				// Save to database immediately
				var currentUser = await _authService.GetCurrentUserAsync();
				if (currentUser != null)
				{
					var playlistModel = new CarrotDownload.Database.Models.PlaylistModel
					{
						ProjectId = _projectId,
						FileName = fileName,
						FilePath = file.FilePath, // Use original file path
						SlotPosition = "", // No default slot
						OrderIndex = playlistFile.Index, // Store 1-based index (matches UI)
						UserId = currentUser.Id,
						CreatedAt = DateTime.UtcNow
					};
					
					await _mongoService.CreatePlaylistAsync(playlistModel);
				}
				
				// Keep the file in the project files UI so the project library persists.
			}
			
			await NotificationService.ShowSuccess("Success! Your files have been added to the playlist.");
			
			// Clear selection + de-highlight cards (no collection reload needed)
			foreach (var projectFile in ProjectFiles.ToList())
			{
				projectFile.IsSelected = false;
			}
			_selectedFiles.Clear();
			AddToPlaylistButton.IsVisible = false;
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't add files to your playlist. Please try again.");
		}
	}

	// Custom Hold-and-Release (Pretend Drag) reordering logic
	private ProjectFileModel? _firstSelectedItem;
	private ProjectFileModel? _currentlyHoveredItem;

	private void OnPlaylistFilePointerPressed(object sender, PointerEventArgs e)
	{
		if (sender is View view && view.BindingContext is ProjectFileModel item)
		{
			_firstSelectedItem = item;
			_firstSelectedItem.IsSwapSelected = true;
			System.Diagnostics.Debug.WriteLine($"[Hold] Pressed: {item.FileName}");
		}
	}

	private void OnPlaylistFilePointerEntered(object sender, PointerEventArgs e)
	{
		if (sender is View view && view.BindingContext is ProjectFileModel item)
		{
			_currentlyHoveredItem = item;
			// Optional: Highlight the target item slightly
		}
	}

	private async void OnPlaylistFilePointerReleased(object sender, PointerEventArgs e)
	{
		try
		{
			if (_firstSelectedItem != null && _currentlyHoveredItem != null && _firstSelectedItem != _currentlyHoveredItem)
			{
				System.Diagnostics.Debug.WriteLine($"[Release] Swapping {_firstSelectedItem.FileName} with {_currentlyHoveredItem.FileName}");
				
				int index1 = FinalizedPlaylistFiles.IndexOf(_firstSelectedItem);
				int index2 = FinalizedPlaylistFiles.IndexOf(_currentlyHoveredItem);

				if (index1 != -1 && index2 != -1)
				{
					// Perform actual swap (replacement)
					FinalizedPlaylistFiles[index1] = _currentlyHoveredItem;
					FinalizedPlaylistFiles[index2] = _firstSelectedItem;
					
					// Re-index for visual consistency
					for (int i = 0; i < FinalizedPlaylistFiles.Count; i++)
					{
						FinalizedPlaylistFiles[i].Index = i + 1;
					}

					await PersistPlaylistOrder();
					await NotificationService.ShowSuccess("The order has been updated successfully!");
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[Pointer] Error: {ex.Message}");
		}
		finally
		{
			if (_firstSelectedItem != null)
			{
				_firstSelectedItem.IsSwapSelected = false;
			}
			_firstSelectedItem = null;
			_currentlyHoveredItem = null;
		}
	}

	private void OnPlaylistFileTapped(object sender, TappedEventArgs e)
	{
		// Keep tap logic for simple selection-to-swap if user doesn't hold-and-move
		if (sender is View view && view.BindingContext is ProjectFileModel clickedItem)
		{
			// Note: Pointer events will likely trigger selection state, 
			// so Tap can act as the 'commit' for the second click approach.
			// However, HandleFileSelection is no longer current logic, 
			// so we can just leave it or integrate it.
		}
	}

	private async Task PersistPlaylistOrder()
	{
		try
		{
			var playlistsFromDb = await _mongoService.GetProjectPlaylistsAsync(_projectId);
			int orderIndex = 1;
			foreach (var file in FinalizedPlaylistFiles)
			{
				var playlist = playlistsFromDb.FirstOrDefault(p => p.FilePath == file.FilePath);
				if (playlist != null)
				{
					await _mongoService.UpdatePlaylistSlotAndOrderAsync(playlist.Id, file.SlotLetter, orderIndex);
				}
				orderIndex++;
			}
		}
		catch (Exception dbEx)
		{
			System.Diagnostics.Debug.WriteLine($"[Persist] Database update error: {dbEx.Message}");
			await NotificationService.ShowError("We couldn't save the order. Please try again.");
		}
	}

	// Playlist handlers
	private async void OnAddSeqClicked(object sender, EventArgs e)
	{
		// Add an inline pending slot (mirrors web multi-block UX)
		PlaylistFiles.Add(new ProjectFileModel
		{
			Index = PlaylistFiles.Count + 1,
			FileName = "No file selected",
			FilePath = string.Empty,
			SlotPosition = -1,
			SlotLetter = string.Empty
		});
	}

	private async void OnUploadPlaylistFilesClicked(object sender, EventArgs e)
	{
		try
		{
			// Consider only blocks that actually have a file
			var validFiles = PlaylistFiles.Where(f => !string.IsNullOrWhiteSpace(f.FilePath)).ToList();

			// Require at least one valid media file
			if (validFiles.Count == 0)
			{
				await NotificationService.ShowError("Please select an audio or video file. Other file types aren't supported.");
				return;
			}

			// Validate media types for provided files; empty blocks are allowed
			if (validFiles.Any(f => !IsValidMediaFile(f.FilePath)))
			{
				await NotificationService.ShowError("Please select an audio or video file. Other file types aren't supported.");
				return;
			}

			// Validate slots in temporary playlist (only if provided)
			var tempSlots = new HashSet<string>();
			foreach (var file in validFiles)
			{
				var slotOriginal = file.SlotLetter?.Trim() ?? "";
				// Check for uppercase letters
				if (!string.IsNullOrEmpty(slotOriginal) && slotOriginal.Any(char.IsUpper))
				{
					await NotificationService.ShowError($"The slot '{file.SlotLetter}' for '{file.FileName}' should be lowercase (a-z). Please update it.");
					return;
				}
				var slot = slotOriginal.ToLower();

				// Empty slot is allowed at upload time; it can be set later.
				if (string.IsNullOrEmpty(slot)) continue;

				if (slot.Length != 1 || slot[0] < 'a' || slot[0] > 'z')
				{
					await NotificationService.ShowError($"The slot for '{file.FileName}' should be a single letter (a-z). Please fix '{file.SlotLetter}'.");
					return;
				}

				if (!tempSlots.Add(slot))
				{
					await NotificationService.ShowError($"The slot '{slot}' is used more than once. Each slot must be unique.");
					return;
				}
			}

			// Check against existing finalized playlists (only for non-empty slots)
			foreach (var file in validFiles)
			{
				var slot = file.SlotLetter?.Trim().ToLower() ?? "";
				if (string.IsNullOrEmpty(slot)) continue;

				if (FinalizedPlaylistFiles.Any(f => (f.SlotLetter ?? "").Trim().ToLower() == slot))
				{
					await NotificationService.ShowError($"The slot '{slot}' is already taken. Please choose a different one.");
					return;
				}
			}

			// Save all playlist files to database using original paths - no file copying
			int nextOrderIndex = FinalizedPlaylistFiles.Count + 1; // 1-based
			foreach (var file in validFiles)
			{
				var fileName = Path.GetFileName(file.FilePath);

				// Create playlist model for database with slot position.
				var playlistModel = new CarrotDownload.Database.Models.PlaylistModel
				{
					ProjectId = _projectId,
					FileName = fileName,
					FilePath = file.FilePath, // Use original file path
					SlotPosition = file.SlotLetter?.Trim().ToLower() ?? "",
					OrderIndex = nextOrderIndex, // 1-based: next sequence number
					IsPrivate = true, // You can get this from the dialog
					CreatedAt = DateTime.UtcNow
				};

				// Save to database
				await _mongoService.CreatePlaylistAsync(playlistModel);

				// Add to finalized collection with slot.
				FinalizedPlaylistFiles.Add(new ProjectFileModel
				{
					Index = nextOrderIndex,
					FileName = fileName,
					FilePath = file.FilePath, // Use original file path
					SlotPosition = file.SlotPosition,
					SlotLetter = file.SlotLetter?.Trim().ToLower() ?? ""
				});

				nextOrderIndex++;
			}

			// Clear temporary playlist
			PlaylistFiles.Clear();

			await NotificationService.ShowSuccess("Perfect! Your playlist files have been uploaded.");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't upload your playlist files. Please try again.");
		}
	}

	private async void OnDeletePlaylistFileClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectFileModel file)
		{
			PlaylistFiles.Remove(file);
			
			// Re-index
			for (int i = 0; i < PlaylistFiles.Count; i++)
			{
				PlaylistFiles[i].Index = i + 1;
			}
		}
	}

	private async void OnChoosePendingFileClicked(object sender, EventArgs e)
	{
		if (sender is VisualElement ve && ve.BindingContext is ProjectFileModel file)
		{
			var pickedPath = await PickMediaFileAsync();
			if (pickedPath == null) return;

			file.FilePath = pickedPath;
			file.FileName = Path.GetFileName(pickedPath);
		}
	}

	private async void OnPendingFileDropped(object sender, DropEventArgs e)
	{
		try
		{
			if (sender is BindableObject bo && bo.BindingContext is ProjectFileModel file)
			{
				var paths = await GetPathsFromDrop(e);
				var first = paths?.FirstOrDefault();
				if (string.IsNullOrWhiteSpace(first) || !File.Exists(first) || !IsValidMediaFile(first))
				{
					await NotificationService.ShowError("Please select an audio or video file. Other file types aren't supported.");
					return;
				}

				// Enforce single file per block
				file.FilePath = first;
				file.FileName = Path.GetFileName(first);
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't add that file. Please try again.");
		}
	}

	private async Task<string?> PickMediaFileAsync()
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
					{ DevicePlatform.WinUI, _allowedExtensions.ToArray() }
				})
			};

			var result = await FilePicker.Default.PickAsync(options);
			if (result == null) return null;

			if (!IsValidMediaFile(result.FullPath))
			{
				await NotificationService.ShowError("Please select an audio or video file. Other file types aren't supported.");
				return null;
			}

			return result.FullPath;
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't select that file. Please try again.");
			return null;
		}
	}

	private bool IsValidMediaFile(string path)
	{
		try
		{
			var ext = Path.GetExtension(path);
			return !string.IsNullOrWhiteSpace(ext) && _allowedExtensions.Contains(ext);
		}
		catch
		{
			return false;
		}
	}

	private async void OnDeleteFinalizedPlaylistFileClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectFileModel file)
		{
			try
			{
				var dialog = new ConfirmationDialog("Delete Playlist File", 
					$"Are you sure you want to delete '{file.FileName}'?", 
					"Delete", "Cancel");
				await Navigation.PushModalAsync(dialog);
				
				if (!await dialog.GetResultAsync()) return;

				// CRITICAL: Delete from database ONLY - NEVER delete user's original files from disk
				// Files are stored at their original locations and must remain untouched
				await _mongoService.DeletePlaylistByFilePathAsync(file.FilePath);

				// Remove from collection
				FinalizedPlaylistFiles.Remove(file);

				// Re-index
				for (int i = 0; i < FinalizedPlaylistFiles.Count; i++)
				{
					FinalizedPlaylistFiles[i].Index = i + 1;
				}

				await NotificationService.ShowSuccess($"'{file.FileName}' has been removed from the playlist.");
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError("We couldn't remove that file. Please try again.");
			}
		}
	}

	private void OnSlotLetterTextChanged(object sender, TextChangedEventArgs e)
	{
		if (sender is BorderlessEntry entry && entry.BindingContext is ProjectFileModel fileModel)
		{
			var newText = e.NewTextValue?.Trim() ?? "";
			
			// Convert to lowercase automatically
			if (!string.IsNullOrEmpty(newText))
			{
				newText = newText.ToLower();
				// Limit to single character
				if (newText.Length > 1)
				{
					newText = newText.Substring(0, 1);
				}
				// Validate it's a letter a-z, if not, clear it
				if (newText.Length == 1 && (newText[0] < 'a' || newText[0] > 'z'))
				{
					newText = string.Empty;
					MainThread.BeginInvokeOnMainThread(() =>
					{
						entry.Text = string.Empty;
						fileModel.SlotLetter = string.Empty;
					});
					return;
				}
			}
			
			// Update the model if the text changed
			if (fileModel.SlotLetter != newText)
			{
				fileModel.SlotLetter = newText;
				// Update the entry text if it was modified (e.g., converted to lowercase)
				if (entry.Text != newText)
				{
					MainThread.BeginInvokeOnMainThread(() => entry.Text = newText);
				}
			}
		}
	}

	private async void OnSaveOrderClicked(object sender, EventArgs e)
	{
		try
		{
			// Validate slot positions (only if provided)
			var slots = new HashSet<string>();
			foreach (var file in FinalizedPlaylistFiles)
			{
				if (!string.IsNullOrEmpty(file.SlotLetter))
				{
					var slotOriginal = file.SlotLetter.Trim();
					// Check for uppercase letters
					if (slotOriginal.Any(char.IsUpper))
					{
						await NotificationService.ShowError($"The slot '{file.SlotLetter}' for '{file.FileName}' should be lowercase (a-z). Please update it.");
						return;
					}
					// Normalize to lowercase
					var slot = slotOriginal.ToLower();
					
					// Validate slot is a-z
					if (slot.Length != 1 || slot[0] < 'a' || slot[0] > 'z')
					{
						await NotificationService.ShowError($"The slot for '{file.FileName}' should be a single letter (a-z). Please fix '{file.SlotLetter}'.");
						return;
					}

					// Check for duplicates
					if (slots.Contains(slot))
					{
						await NotificationService.ShowError($"The slot '{slot}' is used more than once. Each file needs a unique slot.");
						return;
					}

					slots.Add(slot);
					file.SlotLetter = slot; // Normalize
					file.SlotPosition = slot[0] - 'a'; // Update index
				}
				else
				{
					file.SlotPosition = -1;
				}
			}

			// Update all playlists in database with slot positions AND file order
			var playlists = await _mongoService.GetProjectPlaylistsAsync(_projectId);
			int orderIndex = 1; // Start from 1 (1-based)
			foreach (var file in FinalizedPlaylistFiles)
			{
				// Find the playlist in database by filepath
				var playlist = playlists.FirstOrDefault(p => p.FilePath == file.FilePath);
				
				if (playlist != null)
				{
					// Update both slot position and order index
					playlist.SlotPosition = file.SlotLetter;
					await _mongoService.UpdatePlaylistSlotAndOrderAsync(playlist.Id, file.SlotLetter, orderIndex);
				}
				
				orderIndex++;
			}

			await NotificationService.ShowSuccess("Perfect! Your playlist order and slots have been saved.");
			
			// Refresh list to show correct order
			await LoadPlaylistsFromDatabase();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't save the playlist order. Please try again.");
		}
	}
	private async Task<ImageSource?> GenerateThumbnailForFile(string filePath)
	{
		try
		{
			var ext = Path.GetExtension(filePath).ToLower();
			var isVideo = new[] { ".mp4", ".mkv", ".avi", ".mov" }.Contains(ext);
			
			if (isVideo)
			{
				var thumbPath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.jpg");
				var generatedPath = await _ffmpegService.GenerateThumbnailAsync(filePath, thumbPath, TimeSpan.FromSeconds(1));
				
				if (!string.IsNullOrEmpty(generatedPath) && File.Exists(generatedPath))
				{
					return ImageSource.FromFile(generatedPath);
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error generating preview: {ex.Message}");
		}
		return null;
	}
}

// Model for project files
public class ProjectFileModel : System.ComponentModel.INotifyPropertyChanged
{
    private int _index;
    private string _fileName = string.Empty;
    private string _filePath = string.Empty;
    private int _slotPosition;
    private string _slotLetter = string.Empty;

    public int Index 
    { 
        get => _index; 
        set { if (_index != value) { _index = value; OnPropertyChanged(); } } 
    }

    public string FileName 
    { 
        get => _fileName; 
        set { if (_fileName != value) { _fileName = value; OnPropertyChanged(); } } 
    }

    public string FilePath 
    { 
        get => _filePath; 
        set { if (_filePath != value) { _filePath = value; OnPropertyChanged(); } } 
    }

    public int SlotPosition 
    { 
        get => _slotPosition; 
        set { if (_slotPosition != value) { _slotPosition = value; OnPropertyChanged(); } } 
    }

    public string SlotLetter 
    { 
        get => _slotLetter ?? string.Empty; 
        set 
        { 
            var newValue = value ?? string.Empty;
            // Only trim whitespace, don't do aggressive validation here to avoid binding loops
            newValue = newValue.Trim();
            if (_slotLetter != newValue) 
            { 
                _slotLetter = newValue; 
                OnPropertyChanged(); 
            } 
        } 
    }

	private ImageSource? _thumbnail;
	public ImageSource? Thumbnail
	{
		get => _thumbnail;
		set { if (_thumbnail != value) { _thumbnail = value; OnPropertyChanged(); } }
	}

	private bool _isDragging;
	public bool IsDragging
	{
		get => _isDragging;
		set { if (_isDragging != value) { _isDragging = value; OnPropertyChanged(); } }
	}

	private bool _isSwapSelected;
	public bool IsSwapSelected
	{
		get => _isSwapSelected;
		set { if (_isSwapSelected != value) { _isSwapSelected = value; OnPropertyChanged(); } }
	}

	private bool _isSelected;
	public bool IsSelected
	{
		get => _isSelected;
		set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
	}

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

using CarrotDownload.Auth.Interfaces;
using System.Collections.ObjectModel;
using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class ProjectDetailPage : ContentPage
{
	private readonly IAuthService _authService;
	private readonly CarrotDownload.Database.CarrotMongoService _mongoService;
	private readonly CarrotDownload.FFmpeg.Interfaces.IFFmpegService _ffmpegService;
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
			await NotificationService.ShowError($"Failed to load playlists: {ex.Message}");
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
			await NotificationService.ShowError($"Failed to load project files: {ex.Message}");
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
			await NotificationService.ShowError($"Error selecting files: {ex.Message}");
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
			await NotificationService.ShowError($"Error dropping files: {ex.Message}");
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
			await NotificationService.ShowInfo("No files selected");
			return;
		}
		
		try
		{
			int uploadCount = 0;
			foreach (var tempFile in TempFiles.ToList())
			{
				// Store original file path - no copying
				uploadCount++;
				
				// Remove from temp list
				TempFiles.Remove(tempFile);
			}
			
			// Clear UI
			TempFilesCollectionView.IsVisible = false;
			UploadFileButton.IsEnabled = false;
			
			// Reload files to show new files
			await LoadProjectFiles();
			
			await NotificationService.ShowSuccess($"{uploadCount} files added successfully!");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Error adding files: {ex.Message}");
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

				// Do NOT delete physical file - just remove from project
				// if (File.Exists(file.FilePath))
				// {
				// 	File.Delete(file.FilePath);
				// }

				// Reload files
				LoadProjectFiles();

				await NotificationService.ShowSuccess($"File '{file.FileName}' removed from project");
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError($"Failed to remove file: {ex.Message}");
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
				await NotificationService.ShowError($"Could not play file: {ex.Message}");
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
					await NotificationService.ShowError("Playlist file not found in database");
				}
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError($"Failed to open file details: {ex.Message}");
			}
		}
	}
	
	// File selection handler (double-click)
	private void OnFileDoubleClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectFileModel file && sender is Border border)
		{
			// Toggle selection
			if (_selectedFiles.Contains(file))
			{
				// Deselect
				_selectedFiles.Remove(file);
				border.Stroke = Color.FromArgb("#e0e0e0");
				border.Shadow = null;
			}
			else
			{
				// Select with green shadow
				_selectedFiles.Add(file);
				border.Stroke = Color.FromArgb("#28a745");
				border.Shadow = new Shadow
				{
					Brush = new SolidColorBrush(Color.FromArgb("#28a745")),
					Radius = 8,
					Opacity = 0.5f
				};
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
			await NotificationService.ShowInfo("No files selected");
			return;
		}
		
		try
		{
			// Get current project to update its Files list
			var project = await _mongoService.GetProjectByIdAsync(_projectId);
			var projectFiles = project?.Files?.ToList() ?? new List<string>();
			
			// Add selected files to permanent playlist (FinalizedPlaylistFiles) and save to database
			// Using original file paths - no file copying
			foreach (var file in _selectedFiles)
			{
				var fileName = Path.GetFileName(file.FilePath);
				
				// Check if already in playlist (using filename check)
				if (!FinalizedPlaylistFiles.Any(p => p.FileName == fileName))
				{
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
					
					// Remove from project's Files list in database
					projectFiles.Remove(file.FilePath);
				}
				
				// Remove from project files UI
				ProjectFiles.Remove(file);
			}
			
			// Update project's Files list in database
			await _mongoService.UpdateProjectFilesAsync(_projectId, projectFiles);
			
			await NotificationService.ShowSuccess("Files added to playlist successfully!");
			
			// Clear selection
			_selectedFiles.Clear();
			AddToPlaylistButton.IsVisible = false;
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to add files to playlist: {ex.Message}");
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
					await NotificationService.ShowSuccess("Order updated");
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
			await NotificationService.ShowError($"Failed to save order: {dbEx.Message}");
		}
	}

	// Playlist handlers
	private async void OnAddSeqClicked(object sender, EventArgs e)
	{
		// Get existing slot positions from BOTH temporary and finalized playlists
		var existingSlots = PlaylistFiles.Select(f => f.SlotPosition)
			.Concat(FinalizedPlaylistFiles.Select(f => f.SlotPosition))
			.ToList();

		System.Diagnostics.Debug.WriteLine($"Opening Add Seq dialog with {existingSlots.Count} existing slots:");
		System.Diagnostics.Debug.WriteLine($"Slots: {string.Join(", ", existingSlots)}");
		System.Diagnostics.Debug.WriteLine($"Temp playlist has {PlaylistFiles.Count} files");
		System.Diagnostics.Debug.WriteLine($"Finalized playlist has {FinalizedPlaylistFiles.Count} files");

		// Open Add Seq dialog
		var dialog = new AddSeqDialog(existingSlots);
		dialog.PlaylistItemAdded += OnPlaylistItemAdded;
		
		await Navigation.PushModalAsync(dialog);
	}

	private void OnPlaylistItemAdded(object sender, PlaylistItemAddedEventArgs e)
	{
		// Add to temporary playlist files collection
		PlaylistFiles.Add(new ProjectFileModel
		{
			Index = PlaylistFiles.Count + 1,
			FileName = e.FileName,
			FilePath = e.FilePath,
			SlotPosition = e.SlotPosition,
			SlotLetter = e.SlotLetter
		});
	}

	private async void OnUploadPlaylistFilesClicked(object sender, EventArgs e)
	{
		if (PlaylistFiles.Count == 0)
		{
			await NotificationService.ShowInfo("Please add files to the playlist first");
			return;
		}

		try
		{
			// Validate no duplicate slots in temporary playlist
			var tempSlots = new HashSet<string>();
			foreach (var file in PlaylistFiles)
			{
				var slot = file.SlotLetter?.ToLower() ?? "";
				if (tempSlots.Contains(slot))
				{
					await NotificationService.ShowError($"Duplicate slot '{slot}' found in playlist. Please fix before uploading.");
					return;
				}
				tempSlots.Add(slot);
			}

			// Check against existing finalized playlists
			foreach (var file in PlaylistFiles)
			{
				var slot = file.SlotLetter?.ToLower() ?? "";
				if (FinalizedPlaylistFiles.Any(f => f.SlotLetter?.ToLower() == slot))
				{
					await NotificationService.ShowError($"Slot '{slot}' already exists in uploaded playlists. Please choose a different slot.");
					return;
				}
			}

			// Save all playlist files to database using original paths - no file copying
			foreach (var file in PlaylistFiles)
			{
				var fileName = Path.GetFileName(file.FilePath);

				// Create playlist model for database
				var playlistModel = new CarrotDownload.Database.Models.PlaylistModel
				{
					ProjectId = _projectId,
					FileName = fileName,
					FilePath = file.FilePath, // Use original file path
					SlotPosition = file.SlotLetter,
					OrderIndex = FinalizedPlaylistFiles.Count + 1, // 1-based: next sequence number
					IsPrivate = true, // You can get this from the dialog
					CreatedAt = DateTime.UtcNow
				};

				// Save to database
				await _mongoService.CreatePlaylistAsync(playlistModel);

				// Add to finalized collection
				FinalizedPlaylistFiles.Add(new ProjectFileModel
				{
					Index = FinalizedPlaylistFiles.Count + 1,
					FileName = fileName,
					FilePath = file.FilePath, // Use original file path
					SlotPosition = file.SlotPosition,
					SlotLetter = file.SlotLetter
				});
			}

			// Clear temporary playlist
			PlaylistFiles.Clear();

			await NotificationService.ShowSuccess("Playlist files uploaded successfully!");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to upload playlist files: {ex.Message}");
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

				// Delete from database
				await _mongoService.DeletePlaylistByFilePathAsync(file.FilePath);

				// Delete file from disk
				if (File.Exists(file.FilePath))
				{
					File.Delete(file.FilePath);
				}

				// Remove from collection
				FinalizedPlaylistFiles.Remove(file);

				// Re-index
				for (int i = 0; i < FinalizedPlaylistFiles.Count; i++)
				{
					FinalizedPlaylistFiles[i].Index = i + 1;
				}

				await NotificationService.ShowSuccess($"Playlist file '{file.FileName}' deleted successfully");
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError($"Failed to delete playlist file: {ex.Message}");
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
					// Normalize to lowercase
					var slot = file.SlotLetter.Trim().ToLower();
					
					// Validate slot is a-z
					if (slot.Length != 1 || slot[0] < 'a' || slot[0] > 'z')
					{
						await NotificationService.ShowError($"Invalid slot '{file.SlotLetter}' for file '{file.FileName}'. Slot must be a single letter from a to z.");
						return;
					}

					// Check for duplicates
					if (slots.Contains(slot))
					{
						await NotificationService.ShowError($"Duplicate slot '{slot}' found. Each file must have a unique slot position.");
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

			await NotificationService.ShowSuccess("Playlist order and slot positions saved successfully!");
			
			// Refresh list to show correct order
			await LoadPlaylistsFromDatabase();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to save order: {ex.Message}");
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
    private string _fileName;
    private string _filePath;
    private int _slotPosition;
    private string _slotLetter;

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
        get => _slotLetter; 
        set { if (_slotLetter != value) { _slotLetter = value; OnPropertyChanged(); } } 
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

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

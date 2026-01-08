using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Maui.Helpers;
using CarrotDownload.Maui.Services;
using CarrotDownload.Maui.Controls;

namespace CarrotDownload.Maui.Views;

public partial class DashboardPage : ContentPage
{
	private readonly IAuthService _authService;
	private readonly CarrotDownload.Database.CarrotMongoService _mongoService;
	private readonly CarrotDownload.FFmpeg.Interfaces.IFFmpegService _ffmpegService;

	// Collection for the list of projects
	public System.Collections.ObjectModel.ObservableCollection<ProjectModel> Projects { get; set; } = new();

	// Collection for selected files before upload
	public System.Collections.ObjectModel.ObservableCollection<SelectedFileModel> SelectedFiles { get; set; } = new();

	// Collection for programming files before upload
	public System.Collections.ObjectModel.ObservableCollection<ProgrammingFileModel> ProgrammingFiles { get; set; } = new();

	// View Visibility Properties (Simple boolean flags for binding)
	public bool IsProjectViewVisible { get; set; } = true;
	public bool IsPlaylistViewVisible { get; set; } = false;

	public DashboardPage(IAuthService authService, 
                        CarrotDownload.Database.CarrotMongoService mongoService,
                        CarrotDownload.FFmpeg.Interfaces.IFFmpegService ffmpegService)
	{
		InitializeComponent();
		_authService = authService;
		_mongoService = mongoService;
		_ffmpegService = ffmpegService;
		
		BindingContext = this;
		
		this.Appearing += OnPageAppearing;
	}


	private async void OnPageAppearing(object? sender, EventArgs e)
	{
		// Load projects from database
		await LoadProjectsFromDatabase();
	}

	// Load projects from database
	private async Task LoadProjectsFromDatabase()
	{
		try
		{
			var currentUser = await _authService.GetCurrentUserAsync();
			if (currentUser == null) return;

			var userProjects = await _mongoService.GetUserProjectsAsync(currentUser.Id);
			
			// Clear and reload
			Projects.Clear();
			int index = 1;
			foreach (var project in userProjects)
			{
				Projects.Add(new ProjectModel
				{
					Index = index++,
					Name = project.Title,
					StoragePath = project.StoragePath,
					ProjectId = project.ProjectId
				});
			}
		}
		catch (Exception ex)
		{
			// Silently fail - don't show error to user
			System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
		}
	}

	private void OnFilesCheckedChanged(object sender, CheckedChangedEventArgs e)
	{
		if (e.Value)
		{
			IsProjectViewVisible = true;
			IsPlaylistViewVisible = false;
			OnPropertyChanged(nameof(IsProjectViewVisible));
			OnPropertyChanged(nameof(IsPlaylistViewVisible));
		}
	}

	private void OnPlaylistCheckedChanged(object sender, CheckedChangedEventArgs e)
	{
		if (e.Value)
		{
			IsProjectViewVisible = false;
			IsPlaylistViewVisible = true;
			OnPropertyChanged(nameof(IsProjectViewVisible));
			OnPropertyChanged(nameof(IsPlaylistViewVisible));
		}
	}

	private List<ProgrammingFilePair> _programmingFilePairs = new();

	// Class to hold file and slot pair
	private class ProgrammingFilePair
	{
		public string FilePath { get; set; }
		public string SlotLetter { get; set; }
		public View Container { get; set; } // Changed from Border to View
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// Only add initial file/slot pair if none exist
		if (_programmingFilePairs.Count == 0)
		{
			AddFileSlotPair();
		}
	}

	// Add a new file/slot pair UI
	private void AddFileSlotPair()
	{
		var pairContainer = new VerticalStackLayout { Spacing = 10 };
		
		// File path storage
		string selectedFilePath = null;
		
		// Drag and Drop File Area
		var fileDropBorder = new Border
		{
			BackgroundColor = Colors.White,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
			Stroke = Color.FromArgb("#999"),
			StrokeThickness = 2,
			StrokeDashArray = new DoubleCollection { 5, 5 },
			Padding = new Thickness(60, 25),
			WidthRequest = 500,
			HeightRequest = 100,
			HorizontalOptions = LayoutOptions.Center
		};

		var fileLabel = new Label
		{
			Text = "Drag and drop some files here, or click to select files",
			FontSize = 13,
			TextColor = Color.FromArgb("#666"),
			VerticalOptions = LayoutOptions.Center
		};

		var fileStack = new HorizontalStackLayout
		{
			Spacing = 15,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Label { Text = "📁", FontSize = 35, VerticalOptions = LayoutOptions.Center },
				fileLabel
			}
		};

		fileDropBorder.Content = fileStack;

		// Thumbnail Preview
		var thumbnailPreview = new Image
		{
			HeightRequest = 90,
			WidthRequest = 160,
			Aspect = Aspect.AspectFill,
			IsVisible = false,
			HorizontalOptions = LayoutOptions.Center,
			Margin = new Thickness(0, 5)
		};

		// Drag and Drop Gesture
		var dropGesture = new DropGestureRecognizer { AllowDrop = true };
		dropGesture.DragOver += (s, e) =>
		{
			e.AcceptedOperation = DataPackageOperation.Copy;
#if WINDOWS
			if (e.PlatformArgs?.DragEventArgs != null)
			{
				e.PlatformArgs.DragEventArgs.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
				e.PlatformArgs.DragEventArgs.DragUIOverride.IsCaptionVisible = true;
				e.PlatformArgs.DragEventArgs.DragUIOverride.Caption = "Drop to select file";
			}
#endif
			fileDropBorder.Stroke = Color.FromArgb("#2196F3");
			fileDropBorder.BackgroundColor = Color.FromArgb("#e3f2fd");
		};

		dropGesture.DragLeave += (s, e) =>
		{
			fileDropBorder.Stroke = Color.FromArgb("#999");
			fileDropBorder.BackgroundColor = Colors.White;
		};

		dropGesture.Drop += async (s, e) =>
		{
			fileDropBorder.Stroke = Color.FromArgb("#999");
			fileDropBorder.BackgroundColor = Colors.White;

			try
			{
				string firstPath = null;
#if WINDOWS
				if (e.PlatformArgs?.DragEventArgs?.DataView != null)
				{
					var dataView = e.PlatformArgs.DragEventArgs.DataView;
					if (dataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
					{
						var items = await dataView.GetStorageItemsAsync();
						var firstItem = items.FirstOrDefault();
						if (firstItem != null) firstPath = firstItem.Path;
					}
				}
#endif
				if (string.IsNullOrEmpty(firstPath) && e.Data.Properties.ContainsKey("FileNames"))
				{
					var filenames = e.Data.Properties["FileNames"] as IEnumerable<string>;
					firstPath = filenames?.FirstOrDefault();
				}

				if (!string.IsNullOrEmpty(firstPath))
				{
					selectedFilePath = firstPath;
					fileLabel.Text = $"✓ Selected: {Path.GetFileName(firstPath)}";
					fileLabel.TextColor = Color.FromArgb("#28a745");
					
					var thumb = await GenerateThumbnailForFile(firstPath);
					if (thumb != null)
					{
						thumbnailPreview.Source = thumb;
						thumbnailPreview.IsVisible = true;
					}
				}
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError($"Error dropping file: {ex.Message}");
			}
		};
		fileDropBorder.GestureRecognizers.Add(dropGesture);

		// File selection tap gesture
		var tapGesture = new TapGestureRecognizer();
		tapGesture.Tapped += async (s, e) =>
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
					selectedFilePath = result.FullPath;
					fileLabel.Text = $"✓ Selected: {Path.GetFileName(result.FullPath)}";
					fileLabel.TextColor = Color.FromArgb("#28a745");
					
					var thumb = await GenerateThumbnailForFile(result.FullPath);
					if (thumb != null)
					{
						thumbnailPreview.Source = thumb;
						thumbnailPreview.IsVisible = true;
					}
				}
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError($"Error selecting file: {ex.Message}");
			}
		};
		fileDropBorder.GestureRecognizers.Add(tapGesture);

		pairContainer.Children.Add(fileDropBorder); 
		pairContainer.Children.Add(thumbnailPreview); 

		// Separator
		pairContainer.Children.Add(new BoxView
		{
			HeightRequest = 1,
			BackgroundColor = Color.FromArgb("#e0e0e0"),
			WidthRequest = 500,
			HorizontalOptions = LayoutOptions.Center,
			Margin = new Thickness(0, 10, 0, 10)
		});

		// Slot Entry wrapped in Border
		var slotEntry = new BorderlessEntry
		{
			Placeholder = "Enter slot position (a-z)",
			PlaceholderColor = Color.FromArgb("#999"),
			TextColor = Color.FromArgb("#333"),
			BackgroundColor = Colors.Transparent,
			HeightRequest = 38
			// WidthRequest removed as it's controlled by Border
		};

		var slotBorder = new Border
		{
			Stroke = Color.FromArgb("#cccccc"),
			StrokeThickness = 1,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
			BackgroundColor = Colors.White,
			Padding = new Thickness(8, 0),
			WidthRequest = 300,
			HorizontalOptions = LayoutOptions.Center,
			Margin = new Thickness(1), // Fix for bottom border clipping
			Content = slotEntry
		};

		pairContainer.Children.Add(slotBorder);

		// Store reference (no outer border)
		var pair = new ProgrammingFilePair
		{
			Container = pairContainer // Direct container, no border wrapper
		};

		// Update pair when slot changes
		slotEntry.TextChanged += (s, e) =>
		{
			pair.SlotLetter = e.NewTextValue?.Trim().ToLower();
			pair.FilePath = selectedFilePath;
		};

		_programmingFilePairs.Add(pair);
		FileSlotPairsContainer.Children.Add(pair.Container);
	}

	// Add Slot button - creates new file/slot pair
	private async void OnAddSlotClicked(object sender, EventArgs e)
	{
		// Validate current pairs before adding new one
		foreach (var pair in _programmingFilePairs)
		{
			if (string.IsNullOrEmpty(pair.FilePath))
			{
				await NotificationService.ShowError("Please select a file for all existing slots before adding a new one");
				return;
			}

			if (string.IsNullOrEmpty(pair.SlotLetter) || pair.SlotLetter.Length != 1 || pair.SlotLetter[0] < 'a' || pair.SlotLetter[0] > 'z')
			{
				await NotificationService.ShowError("Please enter a valid slot (a-z) for all existing slots");
				return;
			}
		}

		// Check for duplicate slots
		var slots = _programmingFilePairs.Select(p => p.SlotLetter).ToList();
		if (slots.Count != slots.Distinct().Count())
		{
			await NotificationService.ShowError("Duplicate slots detected. Each slot must be unique");
			return;
		}

		// Add new pair
		AddFileSlotPair();
	}

	// Delete programming file from list
	private void OnDeleteProgrammingFileClicked(object sender, TappedEventArgs e)
	{
		// Not needed anymore
	}

	// Upload Programming Files - saves all pairs to database
	private async void OnUploadProgrammingFilesClicked(object sender, EventArgs e)
	{
		string programTitle = ProgramTitleEntry.Text?.Trim() ?? "";
		if (string.IsNullOrEmpty(programTitle))
		{
			await NotificationService.ShowError("Please enter a program title");
			return;
		}

		if (_programmingFilePairs.Count == 0)
		{
			await NotificationService.ShowError("Please add at least one file with a slot");
			return;
		}

		// Validate all pairs
		foreach (var pair in _programmingFilePairs)
		{
			if (string.IsNullOrEmpty(pair.FilePath))
			{
				await NotificationService.ShowError("Please select a file for all slots");
				return;
			}

			if (string.IsNullOrEmpty(pair.SlotLetter) || pair.SlotLetter.Length != 1 || pair.SlotLetter[0] < 'a' || pair.SlotLetter[0] > 'z')
			{
				await NotificationService.ShowError("Please enter valid slots (a-z) for all files");
				return;
			}
		}

		// Check for duplicate slots ONLY within current programming
		var slots = _programmingFilePairs.Select(p => p.SlotLetter).ToList();
		if (slots.Count != slots.Distinct().Count())
		{
			await NotificationService.ShowError("Duplicate slots detected. Each slot must be unique within this programming");
			return;
		}

		// Get current user
		var currentUser = await _authService.GetCurrentUserAsync();
		if (currentUser == null)
		{
			await NotificationService.ShowError("User not authenticated");
			return;
		}

		try
		{
			// Save all pairs to database (use original paths)
			foreach (var pair in _programmingFilePairs)
			{
				var programmingFile = new Database.Models.ProgrammingFileModel
				{
					ProgramTitle = programTitle,
					FileName = Path.GetFileName(pair.FilePath),
					FilePath = pair.FilePath, // Use Original Path
					SlotPosition = pair.SlotLetter,
					IsPrivate = ProgramPrivateRadio.IsChecked,
					UserId = currentUser.Id,
					CreatedAt = DateTime.UtcNow
				};

				await _mongoService.CreateProgrammingFileAsync(programmingFile);
			}

			await NotificationService.ShowSuccess($"Programming '{programTitle}' uploaded with {_programmingFilePairs.Count} files");

			// Reset
			ProgramTitleEntry.Text = "";
			_programmingFilePairs.Clear();
			FileSlotPairsContainer.Children.Clear();
			AddFileSlotPair(); // Add initial pair
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to upload: {ex.Message}");
		}
	}

	// File Picker Logic - Add to collection for preview
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
					await AddToSelectedFiles(result.FullPath);
				}
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Error selecting files: {ex.Message}");
		}
	}

	private async Task AddToSelectedFiles(string filePath)
	{
		if (SelectedFiles.Any(f => f.FilePath == filePath)) return;

		var newFile = new SelectedFileModel
		{
			Index = SelectedFiles.Count + 1,
			FileName = Path.GetFileName(filePath),
			FilePath = filePath,
			FileSize = GetFileSize(filePath)
		};

		SelectedFiles.Add(newFile);

		// Generate thumbnail asynchronously
		var thumb = await GenerateThumbnailForFile(filePath);
		if (thumb != null)
		{
			newFile.Thumbnail = thumb;
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
            e.PlatformArgs.DragEventArgs.DragUIOverride.Caption = "Drop to add files";
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
			var paths = new List<string>();

#if WINDOWS
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
                else if (e.Data.Properties.ContainsKey("FileNames"))
                {
                    // Fallback to properties if StorageItems fails
                    var fileNames = e.Data.Properties["FileNames"] as IEnumerable<string>;
                    if (fileNames != null) paths.AddRange(fileNames);
                }
            }
#else
            // Other platforms fallback (if applicable)
            if (e.Data.Properties.ContainsKey("FileNames"))
            {
                var fileNames = e.Data.Properties["FileNames"] as IEnumerable<string>;
                if (fileNames != null) paths.AddRange(fileNames);
            }
#endif

			if (paths.Any())
			{
				foreach (var path in paths)
				{
					await AddToSelectedFiles(path);
				}
			}
			else 
			{
				System.Diagnostics.Debug.WriteLine("No file paths found in dropped data");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error in OnFilesDropped: {ex}");
			await NotificationService.ShowError($"Error dropping files: {ex.Message}");
		}
	}

	// Remove file from upload queue
	private void OnRemoveFileClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is SelectedFileModel file)
		{
			SelectedFiles.Remove(file);
			
			// Re-index remaining files
			for (int i = 0; i < SelectedFiles.Count; i++)
			{
				SelectedFiles[i].Index = i + 1;
			}
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
			else if (new[] { ".mp3", ".wav", ".flac", ".aac" }.Contains(ext))
			{
				// Return a generic audio icon or similar
				// For now let's just return null and use a placeholder in XAML
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error generating preview: {ex.Message}");
		}
		return null;
	}

	private string GetFileSize(string filePath)
	{
		try
		{
			var fileInfo = new FileInfo(filePath);
			var sizeInMB = fileInfo.Length / (1024.0 * 1024.0);
			return $"{sizeInMB:F2} MB";
		}
		catch
		{
			return "Unknown";
		}
	}

	// Get or set storage path
	private async Task<string> GetStoragePathAsync()
	{
		return PathHelper.GetProjectsPath();
	}

	// Create unique programming folder
	private string CreateProgrammingFolder(string programmingId, string programTitle)
	{
		var basePath = PathHelper.GetProgrammingPath();

		// Sanitize title for folder name
		var sanitizedTitle = string.Join("_", programTitle.Split(Path.GetInvalidFileNameChars()));
		
		// Create folder with format: {ProgrammingId}_{ProgramTitle}
		var folderName = $"{programmingId}_{sanitizedTitle}";
		var folderPath = Path.Combine(basePath, folderName);

		if (!Directory.Exists(folderPath))
		{
			Directory.CreateDirectory(folderPath);
		}

		return folderPath;
	}

	// Create unique project folder
	private string CreateProjectFolder(string projectId, string projectTitle)
	{
		var basePath = PathHelper.GetProjectsPath();

		// Sanitize title for folder name
		var sanitizedTitle = string.Join("_", projectTitle.Split(Path.GetInvalidFileNameChars()));
		
		// Create folder with format: {ProjectId}_{ProjectTitle}
		var folderName = $"{projectId}_{sanitizedTitle}";
		var projectFolderPath = Path.Combine(basePath, folderName);

		if (!Directory.Exists(projectFolderPath))
		{
			Directory.CreateDirectory(projectFolderPath);
		}

		return projectFolderPath;
	}

	// ... (Existing Event Handlers)

	private async void OnLogoutClicked(object sender, EventArgs e)
	{
		var dialog = new ConfirmationDialog("Logout", "Are you sure you want to logout?", "Yes", "No");
		await Navigation.PushModalAsync(dialog);
		if (await dialog.GetResultAsync())
		{
			await _authService.LogoutAsync();
			Application.Current!.MainPage = new AppShell();
		}
	}

	// Navigation Methods
	private void OnHomeClicked(object sender, EventArgs e)
	{
		// Already on home/dashboard
	}

	private async void OnStoreClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//StorePage");
	}

	private async void OnDigiClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//DigiPage");
	}

	private async void OnManagerClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//ManagerPage");
	}

	private async void OnCheckoutClicked(object sender, EventArgs e)
	{
		// Navigate to checkout page (to be created)
		await NotificationService.ShowInfo("Checkout page coming soon!");
	}

	private async void OnAuctionClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//AuctionPage");
	}

	private async void OnSettingsClicked(object sender, EventArgs e)
	{
		// Navigate to settings page (to be created)
		await NotificationService.ShowInfo("Settings page coming soon!");
	}

	private async void OnProgrammingClicked(object sender, TappedEventArgs e)
	{
		await Shell.Current.GoToAsync("//ProgrammingPage");
	}

	private async void OnSequenceClicked(object sender, TappedEventArgs e)
	{
		await Shell.Current.GoToAsync("//SequencePage");
	}

	private async void OnDownloadsClicked(object sender, TappedEventArgs e)
	{
		await Shell.Current.GoToAsync("//DownloadsPage");
	}


	// Button Click Handlers
	private async void OnUploadProgrammingClicked(object sender, EventArgs e)
	{
		var title = ProgramTitleEntry.Text;
		if (string.IsNullOrWhiteSpace(title))
		{
			await NotificationService.ShowError("Please enter a program title");
			return;
		}

		var isPrivate = ProgramPrivateRadio.IsChecked;
		await NotificationService.ShowInfo($"Uploading program: {title} ({(isPrivate ? "Private" : "Public")})");
	}

	private async void OnUploadProjectClicked(object sender, EventArgs e)
	{
		var title = ProjectTitleEntry.Text;
		if (string.IsNullOrWhiteSpace(title))
		{
			await NotificationService.ShowError("Please enter a project title");
			return;
		}

		if (SelectedFiles.Count == 0)
		{
			await NotificationService.ShowError("Please select at least one file to upload");
			return;
		}

		try
		{
			var isPrivate = ProjectPrivateRadio.IsChecked;
			
			// Generate unique project ID
			var projectId = Guid.NewGuid().ToString("N").Substring(0, 8);
			
			// Use original paths
			var savedFilePaths = new List<string>();
			foreach (var file in SelectedFiles)
			{
				savedFilePaths.Add(file.FilePath);
			}

			// Get current user ID
			var currentUser = await _authService.GetCurrentUserAsync();
			var userId = currentUser?.Id ?? "unknown";

			// Create project for database
			var project = new CarrotDownload.Database.Models.ProjectModel
			{
				ProjectId = projectId,
				Title = title,
				IsPrivate = isPrivate,
				StoragePath = "", // No storage path (using original files)
				CreatedAt = DateTime.UtcNow,
				Files = savedFilePaths,
				UserId = userId
			};

			// Save to MongoDB
			await _mongoService.CreateProjectAsync(project);

			// Add to the UI list
			Projects.Add(new ProjectModel 
			{ 
				Index = Projects.Count + 1,
				Name = title,
				StoragePath = "",
				ProjectId = projectId
			});

			// Clear inputs and selected files
			ProjectTitleEntry.Text = string.Empty;
			SelectedFiles.Clear();

			await NotificationService.ShowSuccess($"Project '{title}' created successfully!");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to create project: {ex.Message}");
		}
	}

	// Delete Project Handler - Remove from database and delete files
	private async void OnDeleteProjectClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectModel project)
		{
			try
			{
				var dialog = new ConfirmationDialog("Delete Project", 
					$"Are you sure you want to delete '{project.Name}'?\n\nThis will delete all project files from your computer.", 
					"Delete", "Cancel");
				await Navigation.PushModalAsync(dialog);
				
				if (!await dialog.GetResultAsync()) return;

				// Delete from database
				var currentUser = await _authService.GetCurrentUserAsync();
				if (currentUser != null)
				{
					// Get the full project from database to get MongoDB ID
					var userProjects = await _mongoService.GetUserProjectsAsync(currentUser.Id);
					var dbProject = userProjects.FirstOrDefault(p => p.ProjectId == project.ProjectId);
					
					if (dbProject != null)
					{
						// Delete all playlists associated with this project
						await _mongoService.DeleteProjectPlaylistsAsync(project.ProjectId);
						
						await _mongoService.DeleteProjectAsync(dbProject.Id);
					}
				}

				// Do NOT delete physical files as they are the user's original files
				// if (!string.IsNullOrEmpty(project.StoragePath) && Directory.Exists(project.StoragePath))
				// {
				// 	Directory.Delete(project.StoragePath, recursive: true);
				// }

				// Remove from UI list
				Projects.Remove(project);
				
				// Re-index
				for (int i = 0; i < Projects.Count; i++)
				{
					Projects[i].Index = i + 1;
				}

				await NotificationService.ShowSuccess($"Project '{project.Name}' deleted successfully");
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError($"Failed to delete project: {ex.Message}");
			}
		}
	}

	// Navigate to Project Detail Page
	private async void OnProjectClicked(object sender, TappedEventArgs e)
	{
		if (e.Parameter is ProjectModel project)
		{
			var projectDetailPage = new ProjectDetailPage(_authService, _mongoService, _ffmpegService);
			projectDetailPage.LoadProject(project.ProjectId, project.Name);
			await Navigation.PushAsync(projectDetailPage);
		}
	}

	private async void OnUploadPlaylistClicked(object sender, EventArgs e)
	{
		var title = PlaylistTitleEntry.Text;
		if (string.IsNullOrWhiteSpace(title))
		{
			await NotificationService.ShowError("Please enter a playlist title");
			return;
		}

		if (SelectedFiles.Count == 0)
		{
			await NotificationService.ShowError("Please select at least one file to upload");
			return;
		}

		try
		{
			// 1. Create a PROJECT entry so it shows on the home page
			var projectId = Guid.NewGuid().ToString("N").Substring(0, 8);
			var currentUser = await _authService.GetCurrentUserAsync();
			var userId = currentUser?.Id ?? "unknown";

			// 2. Creates Project Model (Empty generic files list, because files are going to Playlists)
			var project = new CarrotDownload.Database.Models.ProjectModel
			{
				ProjectId = projectId,
				Title = title,
				IsPrivate = true, // Default to private for playlist-created projects
				StoragePath = "", // No storage path
				CreatedAt = DateTime.UtcNow,
				Files = new List<string>(), // No generic project files
				UserId = userId
			};

			// Save Project to DB
			await _mongoService.CreateProjectAsync(project);

			// 3. Process files as PLAYLIST items
			int slotNumber = 0;
			foreach (var file in SelectedFiles)
			{
				// Create Playlist Entry linked to this Project (Using Original Path)
				var playlistFile = new CarrotDownload.Database.Models.PlaylistModel
				{
					ProjectId = projectId, // Link to the new Project
					FileName = Path.GetFileName(file.FilePath),
					FilePath = file.FilePath, // Original Path
					SlotPosition = ((char)('a' + slotNumber)).ToString(),
					UserId = userId,
					CreatedAt = DateTime.UtcNow
				};

				await _mongoService.CreatePlaylistAsync(playlistFile);
				slotNumber++;
			}

			// 4. Update UI - Add to Projects list so it's visible immediately
			Projects.Add(new ProjectModel 
			{ 
				Index = Projects.Count + 1,
				Name = title,
				StoragePath = "",
				ProjectId = projectId
			});

			// Clear inputs
			PlaylistTitleEntry.Text = string.Empty;
			SelectedFiles.Clear();

			await NotificationService.ShowSuccess($"Project '{title}' created with {slotNumber} playlist file(s)!");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to create playlist project: {ex.Message}");
		}
	}

	// Helper to reuse project folder creation logic if needed, 
	// but we already have CreateProjectFolder method in this class.
}

// Simple Model for the Project List
public class ProjectModel : System.ComponentModel.INotifyPropertyChanged
{
	private int _index;
	public int Index 
	{ 
		get => _index;
		set { _index = value; OnPropertyChanged(nameof(Index)); }
	}
	public string Name { get; set; }
	public string StoragePath { get; set; } // Path where project files are stored
	public string ProjectId { get; set; } // Unique project identifier

	public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged(string name) => 
		PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

// Model for selected files before upload
public class SelectedFileModel : System.ComponentModel.INotifyPropertyChanged
{
	private Microsoft.Maui.Controls.ImageSource? _thumbnail;
	public Microsoft.Maui.Controls.ImageSource? Thumbnail 
	{ 
		get => _thumbnail; 
		set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
	}

	public int Index { get; set; }
	public string FileName { get; set; }
	public string FilePath { get; set; }
	public string FileSize { get; set; }

	public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged(string name) => 
		PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

// Model for programming files with slots
public class ProgrammingFileModel : System.ComponentModel.INotifyPropertyChanged
{
	private Microsoft.Maui.Controls.ImageSource? _thumbnail;
	public Microsoft.Maui.Controls.ImageSource? Thumbnail 
	{ 
		get => _thumbnail; 
		set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
	}

	public string FileName { get; set; }
	public string FilePath { get; set; }
	public string SlotLetter { get; set; }

	public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged(string name) => 
		PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

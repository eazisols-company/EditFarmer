using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Maui.Helpers;
using CarrotDownload.Maui.Services;
using CarrotDownload.Maui.Controls;
using System.Linq;
using System.Collections.Generic;

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
		
		// Create pair early so it's available in event handlers
		var pair = new ProgrammingFilePair
		{
			Container = pairContainer
		};
		
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
				if (e.Data.Properties.ContainsKey("FileNames"))
				{
					var filenames = e.Data.Properties["FileNames"] as IEnumerable<string>;
					firstPath = filenames?.FirstOrDefault();
				}

				if (!string.IsNullOrEmpty(firstPath))
				{
					selectedFilePath = firstPath;
					pair.FilePath = firstPath; // Update pair immediately when file is selected
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
				await NotificationService.ShowError("We couldn't add that file. Please try again.");
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
					pair.FilePath = result.FullPath; // Update pair immediately when file is selected
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
				await NotificationService.ShowError("We couldn't select that file. Please try again.");
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

		// Update pair when slot changes
		slotEntry.TextChanged += (s, e) =>
		{
			var trimmedValue = e.NewTextValue?.Trim() ?? "";
			// Check for uppercase letters
			if (!string.IsNullOrEmpty(trimmedValue) && trimmedValue.Any(char.IsUpper))
			{
				// Show error and clear the entry
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await NotificationService.ShowError("Please use lowercase letters (a-z) for slots. Capital letters aren't allowed.");
					slotEntry.Text = "";
					pair.SlotLetter = "";
				});
				return;
			}
			pair.SlotLetter = trimmedValue.ToLower();
			pair.FilePath = selectedFilePath;
		};

		_programmingFilePairs.Add(pair);
		FileSlotPairsContainer.Children.Add(pair.Container);
	}

	// Add Slot button - creates new file/slot pair
	private async void OnAddSlotClicked(object sender, EventArgs e)
	{
		// Validate current pairs before adding new one
		// Only check pairs that have files (slots are optional)
		foreach (var pair in _programmingFilePairs)
		{
			// If a pair has a slot but no file, that's an issue
			if (!string.IsNullOrEmpty(pair.SlotLetter?.Trim()) && string.IsNullOrEmpty(pair.FilePath))
			{
				await NotificationService.ShowError("Please add a file to the current slot before creating a new one.");
				return;
			}

			// Slot is optional here; if provided, validate format.
			var slotOriginal = pair.SlotLetter?.Trim() ?? "";
			// Check for uppercase letters
			if (!string.IsNullOrEmpty(slotOriginal) && slotOriginal.Any(char.IsUpper))
			{
				await NotificationService.ShowError("Please use lowercase letters (a-z) for slots. Capital letters aren't allowed.");
				return;
			}
			var slot = slotOriginal.ToLower();
			if (!string.IsNullOrEmpty(slot) && (slot.Length != 1 || slot[0] < 'a' || slot[0] > 'z'))
			{
				await NotificationService.ShowError("Slots should be a single letter from a to z.");
				return;
			}
		}

		// Check for duplicate slots (ignoring empty)
		var slots = _programmingFilePairs
			.Select(p => p.SlotLetter?.Trim().ToLower() ?? "")
			.Where(s => !string.IsNullOrEmpty(s))
			.ToList();
		if (slots.Count != slots.Distinct().Count())
		{
			await NotificationService.ShowError("Each slot letter needs to be unique. Please check for duplicates.");
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
			await NotificationService.ShowError("Don't forget to add a title for your program!");
			return;
		}

		if (_programmingFilePairs.Count == 0)
		{
			await NotificationService.ShowError("You need to add at least one file to continue.");
			return;
		}

		// Filter out empty pairs (no file and no slot) and validate remaining pairs
		var validPairs = _programmingFilePairs
			.Where(p => !string.IsNullOrEmpty(p.FilePath) || !string.IsNullOrEmpty(p.SlotLetter?.Trim()))
			.ToList();

		if (validPairs.Count == 0)
		{
			await NotificationService.ShowError("You need to add at least one file to continue.");
			return;
		}

		// Validate all pairs that have files
		foreach (var pair in validPairs)
		{
			if (string.IsNullOrEmpty(pair.FilePath))
			{
				await NotificationService.ShowError("Each entry needs a file. Please add one to continue.");
				return;
			}

			// Slot is optional; if provided, validate format.
			var slotOriginal = pair.SlotLetter?.Trim() ?? "";
			// Check for uppercase letters
			if (!string.IsNullOrEmpty(slotOriginal) && slotOriginal.Any(char.IsUpper))
			{
				await NotificationService.ShowError("Please use lowercase letters (a-z) for slots. Capital letters aren't allowed.");
				return;
			}
			var slot = slotOriginal.ToLower();
			if (!string.IsNullOrEmpty(slot) && (slot.Length != 1 || slot[0] < 'a' || slot[0] > 'z'))
			{
				await NotificationService.ShowError("Slots should be a single letter from a to z.");
				return;
			}
		}

		// Check for duplicate slots ONLY within current programming (ignoring empty)
		var slots = _programmingFilePairs
			.Select(p => p.SlotLetter?.Trim().ToLower() ?? "")
			.Where(s => !string.IsNullOrEmpty(s))
			.ToList();
		if (slots.Count != slots.Distinct().Count())
		{
			await NotificationService.ShowError("Each slot letter needs to be unique. Please check for duplicates.");
			return;
		}

		// Get current user
		var currentUser = await _authService.GetCurrentUserAsync();
		if (currentUser == null)
		{
			await NotificationService.ShowError("Please log in to continue.");
			return;
		}

		try
		{
			// Save all valid pairs to database (use original paths)
			foreach (var pair in validPairs)
			{
				var programmingFile = new Database.Models.ProgrammingFileModel
				{
					ProgramTitle = programTitle,
					FileName = Path.GetFileName(pair.FilePath),
					FilePath = pair.FilePath, // Use Original Path
					SlotPosition = pair.SlotLetter?.Trim().ToLower() ?? "",
					IsPrivate = ProgramPrivateRadio.IsChecked,
					UserId = currentUser.Id,
					CreatedAt = DateTime.UtcNow
				};

				await _mongoService.CreateProgrammingFileAsync(programmingFile);
			}

			await NotificationService.ShowSuccess($"Success! '{programTitle}' has been uploaded with {validPairs.Count} file(s).");

			// Reset
			ProgramTitleEntry.Text = "";
			_programmingFilePairs.Clear();
			FileSlotPairsContainer.Children.Clear();
			AddFileSlotPair(); // Add initial pair
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't upload your files. Please try again.");
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
			if (results == null || !results.Any())
				return;

			var filePaths = results.Select(r => r.FullPath).ToList();

			int index = SelectedFiles.Count + 1;
			foreach (var filePath in filePaths)
			{
				AddToSelectedFilesSync(filePath, index++);
			}

			foreach (var filePath in filePaths)
			{
				var path = filePath;
				_ = Task.Run(async () =>
				{
					var file = SelectedFiles.FirstOrDefault(f => f.FilePath == path);
					if (file != null)
					{
						var thumb = await GenerateThumbnailForFile(path);
						if (thumb != null)
						{
							MainThread.BeginInvokeOnMainThread(() =>
							{
								file.Thumbnail = thumb;
							});
						}
					}
				});
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't select those files. Please try again.");
		}
	}

	private void AddToSelectedFilesSync(string filePath, int index = -1)
	{
		if (SelectedFiles.Any(f => f.FilePath == filePath)) return;

		int fileIndex = index > 0 ? index : SelectedFiles.Count + 1;

		var newFile = new SelectedFileModel
		{
			Index = fileIndex,
			FileName = Path.GetFileName(filePath),
			FilePath = filePath,
			FileSize = GetFileSize(filePath)
		};

		SelectedFiles.Add(newFile);
	}

	private async Task AddToSelectedFiles(string filePath)
	{
		AddToSelectedFilesSync(filePath);

		var file = SelectedFiles.FirstOrDefault(f => f.FilePath == filePath);
		if (file != null)
		{
			var thumb = await GenerateThumbnailForFile(filePath);
			if (thumb != null)
			{
				file.Thumbnail = thumb;
			}
		}
	}

	private void OnDragOver(object sender, DragEventArgs e)
	{
		e.AcceptedOperation = DataPackageOperation.Copy;
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
			if (e.Data.Properties.ContainsKey("FileNames"))
			{
				var fileNames = e.Data.Properties["FileNames"] as IEnumerable<string>;
				if (fileNames != null)
					paths.AddRange(fileNames);
			}

			if (!paths.Any())
				return;

			var validPaths = paths.Where(p => !string.IsNullOrEmpty(p)).ToList();
			int index = SelectedFiles.Count + 1;
			foreach (var path in validPaths)
			{
				AddToSelectedFilesSync(path, index++);
			}

			foreach (var path in validPaths)
			{
				var filePath = path;
				_ = Task.Run(async () =>
				{
					var file = SelectedFiles.FirstOrDefault(f => f.FilePath == filePath);
					if (file != null)
					{
						var thumb = await GenerateThumbnailForFile(filePath);
						if (thumb != null)
						{
							MainThread.BeginInvokeOnMainThread(() =>
							{
								file.Thumbnail = thumb;
							});
						}
					}
				});
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't add those files. Please try again.");
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
		await NotificationService.ShowInfo("The checkout feature is coming soon!");
	}

	private async void OnAuctionClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//AuctionPage");
	}

	private async void OnSettingsClicked(object sender, EventArgs e)
	{
		// Navigate to settings page (to be created)
		await NotificationService.ShowInfo("Settings are coming soon!");
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
			await NotificationService.ShowError("Don't forget to add a title for your program!");
			return;
		}

		var isPrivate = ProgramPrivateRadio.IsChecked;
		await NotificationService.ShowInfo($"Uploading your {(isPrivate ? "private" : "public")} program: {title}");
	}

	private async void OnUploadProjectClicked(object sender, EventArgs e)
	{
		var title = ProjectTitleEntry.Text;
		if (string.IsNullOrWhiteSpace(title))
		{
			await NotificationService.ShowError("Don't forget to add a title for your project!");
			return;
		}

		if (SelectedFiles.Count == 0)
		{
			await NotificationService.ShowError("Please select at least one file to get started.");
			return;
		}

		try
		{
			var isPrivate = ProjectPrivateRadio.IsChecked;
			
			// Generate unique project ID
			var projectId = Guid.NewGuid().ToString("N").Substring(0, 8);
			
			// Use original paths - ensure we iterate in Index order
			var savedFilePaths = SelectedFiles
				.OrderBy(f => f.Index)
				.Select(f => f.FilePath)
				.ToList();

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

			await NotificationService.ShowSuccess($"Awesome! Your project '{title}' has been created.");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't create your project. Please try again.");
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

				// Refresh list from database to ensure sync
				await LoadProjectsFromDatabase();

				await NotificationService.ShowSuccess($"'{project.Name}' has been deleted.");
			}
			catch (Exception ex)
			{
				await NotificationService.ShowError("We couldn't delete that project. Please try again.");
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
			await NotificationService.ShowError("Don't forget to add a title for your playlist!");
			return;
		}

		if (SelectedFiles.Count == 0)
		{
			await NotificationService.ShowError("Please select at least one file to get started.");
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

			// 3. Process files as PLAYLIST items in Index order
			//    Do NOT auto-assign slot letters. Slots should only be set explicitly later.
			var orderedFiles = SelectedFiles.OrderBy(f => f.Index).ToList();
			foreach (var file in orderedFiles)
			{
				// Create Playlist Entry linked to this Project (Using Original Path)
				var playlistFile = new CarrotDownload.Database.Models.PlaylistModel
				{
					ProjectId = projectId, // Link to the new Project
					FileName = Path.GetFileName(file.FilePath),
					FilePath = file.FilePath, // Original Path
					SlotPosition = string.Empty, // No auto slot; user can set it manually later
					UserId = userId,
					CreatedAt = DateTime.UtcNow
				};

				await _mongoService.CreatePlaylistAsync(playlistFile);
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
			var fileCount = SelectedFiles.Count;
			SelectedFiles.Clear();

			await NotificationService.ShowSuccess($"Great! '{title}' has been created with {fileCount} file(s).");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't create your playlist. Please try again.");
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

// Natural string comparer for sorting filenames (handles numbers correctly)
// Example: "barn1", "barn2", "barn10" instead of "barn1", "barn10", "barn2"
public class NaturalStringComparer : IComparer<string>
{
	public int Compare(string? x, string? y)
	{
		if (x == null && y == null) return 0;
		if (x == null) return -1;
		if (y == null) return 1;

		x = x.ToLowerInvariant();
		y = y.ToLowerInvariant();

		int i = 0, j = 0;
		while (i < x.Length && j < y.Length)
		{
			if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
			{
				int numX = 0, numY = 0;
				while (i < x.Length && char.IsDigit(x[i]))
				{
					numX = numX * 10 + (x[i] - '0');
					i++;
				}
				while (j < y.Length && char.IsDigit(y[j]))
				{
					numY = numY * 10 + (y[j] - '0');
					j++;
				}
				if (numX != numY)
					return numX.CompareTo(numY);
			}
			else
			{
				int cmp = x[i].CompareTo(y[j]);
				if (cmp != 0)
					return cmp;
				i++;
				j++;
			}
		}
		return x.Length.CompareTo(y.Length);
	}
}

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using CarrotDownload.Database;
using CarrotDownload.Database.Models;
using CarrotDownload.Auth.Interfaces;
using System.Collections.ObjectModel;
using IO = System.IO;
using System;
using System.Linq;
using CarrotDownload.Maui.Controls;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class ProgrammingPage : ContentPage
{
	private readonly CarrotMongoService _mongoService;
	private readonly IAuthService _authService;
	private string _selectedProgramTitle;
	private List<Border> _programTitleButtons = new();
	public ObservableCollection<Database.Models.ProgrammingFileModel> SelectedProgramFiles { get; set; } = new();
	private Dictionary<string, int> _dotClickCounts = new(); // Track dot clicks for deletion
	private Database.Models.ProjectModel _selectedPlaylist; // Track selected playlist
	private Border _selectedPlaylistBorder; // Track selected playlist border for visual feedback
	
	public ProgrammingPage(CarrotMongoService mongoService, IAuthService authService)
	{
		InitializeComponent();
		_mongoService = mongoService;
		_authService = authService;
		BindingContext = this;
	}
	
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadProgrammingFiles();
	}
	
	private async Task LoadProgrammingFiles()
	{
		try
		{
			// Get current user
			var currentUser = await _authService.GetCurrentUserAsync();
			if (currentUser == null) return;
			
			// Get all programming files for this user
			var programmingFiles = await _mongoService.GetUserProgrammingFilesAsync(currentUser.Id);
			
			// Group by program title
			var groupedPrograms = programmingFiles.GroupBy(f => f.ProgramTitle).ToList();
			
			// Clear and rebuild program titles tabs
			ProgramTitlesContainer.Children.Clear();
			_programTitleButtons.Clear();
			
			foreach (var program in groupedPrograms)
			{
				var button = new Border
				{
					BackgroundColor = Color.FromArgb("#e0e0e0"), // Default Grey
					StrokeShape = new RoundRectangle { CornerRadius = 6 },
					Padding = new Thickness(20, 10),
					Margin = new Thickness(5, 0)
				};
				
				var label = new Label
				{
					Text = program.Key,
					TextColor = Color.FromArgb("#333333"), // Dark text for light bg
					FontSize = 14,
					FontAttributes = FontAttributes.Bold
				};
				
				button.Content = label;
				
				var tapGesture = new TapGestureRecognizer();
				string programTitle = program.Key;
				tapGesture.Tapped += (s, e) => OnProgramTitleSelected(programTitle, programmingFiles, button);
				button.GestureRecognizers.Add(tapGesture);
				
				_programTitleButtons.Add(button);
				ProgramTitlesContainer.Children.Add(button);
			}
			
			// Don't select any program by default
			ProgramDetailsContainer.IsVisible = false;
			DetailsHeaderLabel.Text = "";
			SelectedProgramFiles.Clear();
			
			// Load user playlists
			await LoadUserPlaylists();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to load programming files: {ex.Message}");
		}
	}
	
	private void OnProgramTitleSelected(string programTitle, List<Database.Models.ProgrammingFileModel> allFiles, Border selectedButton)
	{
		ProgramDetailsContainer.IsVisible = true;
		_selectedProgramTitle = programTitle;
		DetailsHeaderLabel.Text = $"Details for: {programTitle}";
		
		// Reset all buttons to default color (Grey)
		foreach (var btn in _programTitleButtons)
		{
			btn.BackgroundColor = Color.FromArgb("#e0e0e0");
			if (btn.Content is Label btnLabel)
			{
				btnLabel.TextColor = Color.FromArgb("#333333");
			}
		}
		
		// Highlight selected button (Purple)
		selectedButton.BackgroundColor = Color.FromArgb("#5e6eea"); 
		if (selectedButton.Content is Label selectedLabel)
		{
			selectedLabel.TextColor = Colors.White;
		}
		
		// Filter files for this program
		var programFiles = allFiles.Where(f => f.ProgramTitle == programTitle).ToList();
		
		SelectedProgramFiles.Clear();
		foreach (var file in programFiles)
		{
			SelectedProgramFiles.Add(file);
		}
		
		// Reset tracking when switching tabs
		_dotClickCounts.Clear();
	}

	private async void OnDotClicked(object sender, TappedEventArgs e)
	{
		if (sender is Label dotLabel && e.Parameter is Database.Models.ProgrammingFileModel file)
		{
			if (!_dotClickCounts.ContainsKey(file.Id))
				_dotClickCounts[file.Id] = 0;
				
			_dotClickCounts[file.Id]++;
			
			if (_dotClickCounts[file.Id] == 1)
			{
				// First click: Turn purple
				dotLabel.TextColor = Color.FromArgb("#800080"); // Purple
			}
			else if (_dotClickCounts[file.Id] >= 2)
			{
				// Second click: Confirm delete
				var dialog = new ConfirmationDialog("Confirm Delete", $"Delete file '{file.FileName}' from programming?", "Delete", "Cancel");
				await Navigation.PushModalAsync(dialog);
				if (await dialog.GetResultAsync())
				{
					try
					{
						// 1. Delete from Database
						await _mongoService.DeleteProgrammingFileAsync(file.Id);
						
						// 2. Delete Physical File
						if (!string.IsNullOrEmpty(file.FilePath) && IO.File.Exists(file.FilePath))
						{
							try 
							{
								IO.File.Delete(file.FilePath);
								Console.WriteLine($"Deleted file: {file.FilePath}");
								
								// 3. Check if folder is empty and delete it
								var folderPath = IO.Path.GetDirectoryName(file.FilePath);
								if (!string.IsNullOrEmpty(folderPath) && IO.Directory.Exists(folderPath))
								{
									// Create Programming folder logic creates specific folders per program
									// So we can safely delete if empty
									if (!IO.Directory.EnumerateFileSystemEntries(folderPath).Any())
									{
										IO.Directory.Delete(folderPath);
										Console.WriteLine($"Deleted empty folder: {folderPath}");
									}
								}
							}
							catch (Exception ex)
							{
								Console.WriteLine($"Warning: Failed to delete file/folder: {ex.Message}");
							}
						}

						// 4. Update UI
						SelectedProgramFiles.Remove(file);
						_dotClickCounts.Remove(file.Id);

						await NotificationService.ShowSuccess("File deleted successfully");

						// 5. If Program is empty, reload entire page to remove the tab
						if (SelectedProgramFiles.Count == 0)
						{
							await NotificationService.ShowInfo("Programming empty. Removing from list.");
							await LoadProgrammingFiles();
						}
					}
					catch (Exception ex)
					{
						await NotificationService.ShowError($"Failed to delete: {ex.Message}");
					}
				}
				else
				{
					// Reset state if canceled
					_dotClickCounts[file.Id] = 0;
					dotLabel.TextColor = Color.FromArgb("#333333"); // Reset to original grey (using dark text color from previous step)
				}
			}
		}
	}
	
	private async Task LoadUserPlaylists()
	{
		try
		{
			// Get current user
			var currentUser = await _authService.GetCurrentUserAsync();
			if (currentUser == null) return;
			
			// Get all projects (which represent playlists)
			var projects = await _mongoService.GetUserProjectsAsync(currentUser.Id);
			
			PlaylistsContainer.Children.Clear();
			
			foreach (var project in projects)
			{
				var border = new Border
				{
					BackgroundColor = Colors.White,
					Stroke = Color.FromArgb("#333"),
					StrokeThickness = 2,
					StrokeShape = new RoundRectangle { CornerRadius = 6 },
					Padding = new Thickness(15, 10),
					Margin = new Thickness(0, 5),
					WidthRequest = 200,
					HorizontalOptions = LayoutOptions.Center
				};
				
				var label = new Label
				{
					Text = project.Title,
					TextColor = Color.FromArgb("#333"),
					FontSize = 16,
					HorizontalOptions = LayoutOptions.Center
				};
				
				border.Content = label;
				
				// Add tap gesture to select playlist
				var tapGesture = new TapGestureRecognizer();
				tapGesture.Tapped += (s, e) => OnPlaylistSelected(project, border, label);
				border.GestureRecognizers.Add(tapGesture);
				
				PlaylistsContainer.Children.Add(border);
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to load playlists: {ex.Message}");
		}
	}
	
	private void OnPlaylistSelected(Database.Models.ProjectModel project, Border border, Label label)
	{
		// Reset previous selection
		if (_selectedPlaylistBorder != null)
		{
			_selectedPlaylistBorder.BackgroundColor = Colors.White;
			_selectedPlaylistBorder.Stroke = Color.FromArgb("#333");
			if (_selectedPlaylistBorder.Content is Label oldLabel)
			{
				oldLabel.TextColor = Color.FromArgb("#333");
			}
		}
		
		// Set new selection
		_selectedPlaylist = project;
		_selectedPlaylistBorder = border;
		
		// Highlight selected playlist (purple)
		border.BackgroundColor = Color.FromArgb("#5e6eea");
		border.Stroke = Color.FromArgb("#5e6eea");
		label.TextColor = Colors.White;
	}
	
	private void OnSlotChanged(object sender, TextChangedEventArgs e)
	{
		// Hide error when user starts typing
		ErrorMessageBorder.IsVisible = false;
		
		if (sender is Entry entry && !string.IsNullOrEmpty(e.NewTextValue))
		{
			// Convert to lowercase
			entry.Text = e.NewTextValue.ToLower();
		}
	}

	private void OnSlotPositionTapped(object sender, EventArgs e)
	{
		if (sender is HorizontalStackLayout layout)
		{
			var entry = layout.Children.OfType<BorderlessEntry>().FirstOrDefault();
			if (entry != null)
			{
				entry.IsReadOnly = false;
				entry.BackgroundColor = Color.FromArgb("#ffffff"); // Show white when editing
				entry.Focus();
			}
		}
	}

	private void OnSlotEntryUnfocused(object sender, FocusEventArgs e)
	{
		if (sender is BorderlessEntry entry)
		{
			entry.IsReadOnly = true;
			entry.BackgroundColor = Colors.Transparent;
		}
	}
	
	private async void OnSaveSlotChangesClicked(object sender, EventArgs e)
	{
		try
		{
			// Validate all slots
			foreach (var file in SelectedProgramFiles)
			{
				if (string.IsNullOrEmpty(file.SlotPosition) || file.SlotPosition.Length != 1 || file.SlotPosition[0] < 'a' || file.SlotPosition[0] > 'z')
				{
					ShowError("All slots must be a single letter from a to z");
					return;
				}
			}
			
			// Check for duplicate slots within this programming
			var slots = SelectedProgramFiles.Select(f => f.SlotPosition).ToList();
			var duplicates = slots.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
			
			if (duplicates.Any())
			{
				ShowError($"Another file already has this slot: {string.Join(", ", duplicates)}");
				return;
			}
			
			// Update all files in database
			foreach (var file in SelectedProgramFiles)
			{
				await _mongoService.UpdateProgrammingFileSlotAsync(file.Id, file.SlotPosition);
			}
			
			// await DisplayAlert("Success", "Slot positions updated successfully", "OK");
			await ShowSuccessMessage("Slot positions updated successfully");
			ErrorMessageBorder.IsVisible = false;
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to save changes: {ex.Message}");
		}
	}
	
	private void ShowError(string message)
	{
		ErrorMessageLabel.Text = message;
		ErrorMessageBorder.IsVisible = true;
	}
	
	private async void OnApplyFilesClicked(object sender, EventArgs e)
	{
		try
		{
			// Validate that a program is selected
			if (string.IsNullOrEmpty(_selectedProgramTitle) || SelectedProgramFiles.Count == 0)
			{
				await NotificationService.ShowError("Please select a programming first");
				return;
			}

			// Validate that a playlist is selected
			if (_selectedPlaylist == null)
			{
				await NotificationService.ShowError("Please select a playlist first");
				return;
			}

			// Get current user
			var currentUser = await _authService.GetCurrentUserAsync();
			if (currentUser == null)
			{
				await NotificationService.ShowError("User not authenticated");
				return;
			}

			var project = _selectedPlaylist;

			// Get existing playlist files
			var existingPlaylistFiles = await _mongoService.GetProjectPlaylistsAsync(project.ProjectId);
			if (existingPlaylistFiles == null || existingPlaylistFiles.Count == 0)
			{
				await NotificationService.ShowError("Selected playlist has no files");
				return;
			}

			// Sort existing files by OrderIndex
			var sortedPlaylistFiles = existingPlaylistFiles.OrderBy(f => f.OrderIndex).ToList();

			// Validate that we have enough programming files
			if (SelectedProgramFiles.Count < sortedPlaylistFiles.Count)
			{
				var dialog = new ConfirmationDialog("Warning", 
					$"Programming has {SelectedProgramFiles.Count} files but playlist has {sortedPlaylistFiles.Count} slots. Continue anyway?", 
					"Continue", "Cancel");
				await Navigation.PushModalAsync(dialog);
				if (!await dialog.GetResultAsync())
					return;
			}

			// Sort programming files by slot position (a, b, c, etc.)
			var sortedProgrammingFiles = SelectedProgramFiles.OrderBy(f => f.SlotPosition).ToList();

			// Confirm the operation
			// bool confirm = await DisplayAlert("Confirm Apply Files",
			// 	$"This will replace {Math.Min(sortedProgrammingFiles.Count, sortedPlaylistFiles.Count)} files in '{project.Title}' with files from '{_selectedProgramTitle}'.\n\nA new version of the playlist will be created. Continue?",
			// 	"Yes", "No");

			// if (!confirm)
			// 	return;

			// Delete old playlist files from database and disk
			foreach (var oldFile in sortedPlaylistFiles)
			{
				// Delete from database
				await _mongoService.DeletePlaylistByIdAsync(oldFile.Id);

				// Delete physical file
				if (!string.IsNullOrEmpty(oldFile.FilePath) && IO.File.Exists(oldFile.FilePath))
				{
					try
					{
						IO.File.Delete(oldFile.FilePath);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Warning: Failed to delete old file {oldFile.FilePath}: {ex.Message}");
					}
				}
			}

			// Create new playlist files by copying programming files
			int filesApplied = 0;
			for (int i = 0; i < sortedPlaylistFiles.Count && i < sortedProgrammingFiles.Count; i++)
			{
				var oldPlaylistFile = sortedPlaylistFiles[i];
				var programmingFile = sortedProgrammingFiles[i];

				// Get the playlist folder path
				var playlistFolderPath = IO.Path.GetDirectoryName(oldPlaylistFile.FilePath);
				if (string.IsNullOrEmpty(playlistFolderPath) || !IO.Directory.Exists(playlistFolderPath))
				{
					Console.WriteLine($"Warning: Playlist folder not found: {playlistFolderPath}");
					continue;
				}

				// Copy the programming file to the playlist folder
				var newFileName = IO.Path.GetFileName(programmingFile.FilePath);
				var newFilePath = IO.Path.Combine(playlistFolderPath, newFileName);

				// Copy the programming file to the playlist folder (overwrite existing)

				// Copy the file
				if (IO.File.Exists(programmingFile.FilePath))
				{
					IO.File.Copy(programmingFile.FilePath, newFilePath, true);

					// Create new playlist entry in database
					var newPlaylistFile = new PlaylistModel
					{
						ProjectId = project.ProjectId,
						FileName = newFileName,
						FilePath = newFilePath,
						SlotPosition = oldPlaylistFile.SlotPosition, // Keep original slot position
						OrderIndex = oldPlaylistFile.OrderIndex, // Keep original order
						UserId = currentUser.Id,
						Notes = new List<string>(), // Start with empty notes
						IsPrivate = project.IsPrivate,
						CreatedAt = DateTime.UtcNow
					};

					await _mongoService.CreatePlaylistAsync(newPlaylistFile);
					filesApplied++;
				}
				else
				{
					Console.WriteLine($"Warning: Programming file not found: {programmingFile.FilePath}");
				}
			}

			var successMessage = "Playlist processed successfully!";
			await ShowSuccessMessage(successMessage);
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to apply files: {ex.Message}");
		}
	}

	private async Task ShowSuccessMessage(string message)
	{
		await NotificationService.ShowSuccess(message);
	}

	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//DashboardPage");
	}
}

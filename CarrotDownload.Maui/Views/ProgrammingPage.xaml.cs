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
	private List<PlaylistSelectionItem> _playlistItems = new(); // Track playlist selections
	
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
			await NotificationService.ShowError("We couldn't load your programming files. Please try again.");
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
		
		// Filter files for this program (sorted by slot position a-z)
		var programFiles = allFiles
			.Where(f => f.ProgramTitle == programTitle)
			.OrderBy(f => GetSlotSortKey(f.SlotPosition))
			.ThenBy(f => f.FileName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
			.ToList();
		
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
						// CRITICAL: Delete from Database ONLY - NEVER delete user's original files from disk
						// Files are stored at their original locations and must remain untouched
						await _mongoService.DeleteProgrammingFileAsync(file.Id);

						// 4. Update UI
						SelectedProgramFiles.Remove(file);
						_dotClickCounts.Remove(file.Id);

						await NotificationService.ShowSuccess("The file has been deleted successfully!");

						// 5. If Program is empty, reload entire page to remove the tab
						if (SelectedProgramFiles.Count == 0)
						{
							await NotificationService.ShowInfo("This programming is now empty and has been removed.");
							await LoadProgrammingFiles();
						}
					}
					catch (Exception ex)
					{
						await NotificationService.ShowError("We couldn't delete that file. Please try again.");
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
			_playlistItems.Clear();
			
			foreach (var project in projects)
			{
				var playlistItem = new PlaylistSelectionItem
				{
					Project = project,
					IsChecked = false
				};
				
				var border = new Border
				{
					BackgroundColor = Colors.White,
					Stroke = Color.FromArgb("#333"),
					StrokeThickness = 2,
					StrokeShape = new RoundRectangle { CornerRadius = 6 },
					Padding = new Thickness(15, 10),
					Margin = new Thickness(0, 5),
					WidthRequest = 220,
					HorizontalOptions = LayoutOptions.Center
				};
				
				var label = new Label
				{
					Text = project.Title,
					TextColor = Color.FromArgb("#333"),
					FontSize = 16,
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center
				};

				// Tap gesture to toggle selection
				var tapGesture = new TapGestureRecognizer();
				tapGesture.Tapped += (s, e) =>
				{
					playlistItem.IsChecked = !playlistItem.IsChecked;
					UpdateApplyFilesButtonState();
					
					// Update visual state of border
					if (playlistItem.IsChecked)
					{
						border.BackgroundColor = Color.FromArgb("#5e6eea");
						border.Stroke = Color.FromArgb("#5e6eea");
						label.TextColor = Colors.White;
					}
					else
					{
						border.BackgroundColor = Colors.White;
						border.Stroke = Color.FromArgb("#333");
						label.TextColor = Color.FromArgb("#333");
					}
				};

				border.GestureRecognizers.Add(tapGesture);
				border.Content = label;
				
				PlaylistsContainer.Children.Add(border);
				_playlistItems.Add(playlistItem);
			}

			UpdateApplyFilesButtonState();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't load your playlists. Please try again.");
		}
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
		// Handle tap on the StackLayout (when clicking label) or the Border (when clicking alphabet box)
		if (sender is View view)
		{
			// If we clicked the Border containing the entry
			if (view is Border directBorder)
			{
				var directEntry = FindSlotEntry(directBorder);
				if (directEntry != null)
				{
					EnableSlotEditing(directBorder, directEntry);
					return;
				}
			}
			
			// If we clicked the stack layout, find the border inside it
			if (view is HorizontalStackLayout layout)
			{
				var border = layout.Children.OfType<Border>().FirstOrDefault();
				if (border != null)
				{
					var entry = FindSlotEntry(border);
					if (entry != null)
					{
						EnableSlotEditing(border, entry);
					}
				}
			}
		}
	}

	private static BorderlessEntry? FindSlotEntry(Border border)
	{
		// New template: Border.Content is a Grid containing placeholder Label + SlotEntry
		if (border.Content is Grid grid)
		{
			return grid.Children.OfType<BorderlessEntry>().FirstOrDefault();
		}

		// Backwards compatibility (older template)
		return border.Content as BorderlessEntry;
	}

	private void EnableSlotEditing(Border border, BorderlessEntry entry)
	{
		// If we're in the "empty slot" state, reveal the entry and hide the placeholder text
		if (border.Content is Grid grid)
		{
			var placeholder = grid.Children.OfType<Label>().FirstOrDefault();
			if (placeholder != null)
				placeholder.IsVisible = false;

			entry.IsVisible = true;
		}

		entry.IsReadOnly = false;
		entry.InputTransparent = false; // Enable input
		
		// Highlight the box
		border.Stroke = Color.FromArgb("#cccccc");
		border.BackgroundColor = Colors.White;
		
		entry.Focus();
	}

	private void OnSlotEntryUnfocused(object sender, FocusEventArgs e)
	{
		if (sender is BorderlessEntry entry)
		{
			entry.IsReadOnly = true;
			entry.InputTransparent = true; // Disable input so taps go to container/border

			// Our visual tree is: Border -> Grid (SlotContainerGrid) -> Label + Entry
			var grid = entry.Parent as Grid;
			var border = grid?.Parent as Border;

			// Remove highlight from the border
			if (border != null)
			{
				border.Stroke = Colors.Transparent;
				border.BackgroundColor = Colors.Transparent;
			}

			// If user left it blank, swap back to the "Click to set position" text
			if (grid != null && string.IsNullOrWhiteSpace(entry.Text))
			{
				entry.IsVisible = false;
				var placeholder = grid.Children.OfType<Label>().FirstOrDefault();
				if (placeholder != null)
					placeholder.IsVisible = true;
			}
		}
	}

	private void OnSlotContainerLoaded(object sender, EventArgs e)
	{
		if (sender is Grid grid && grid.BindingContext is Database.Models.ProgrammingFileModel file)
		{
			var placeholder = grid.Children.OfType<Label>().FirstOrDefault();
			var entry = grid.Children.OfType<BorderlessEntry>().FirstOrDefault();
			if (placeholder == null || entry == null)
				return;

			// Initial state based on whether a slot is already set
			if (string.IsNullOrWhiteSpace(file.SlotPosition))
			{
				placeholder.IsVisible = true;
				entry.IsVisible = false;
				entry.Text = string.Empty;
			}
			else
			{
				placeholder.IsVisible = false;
				entry.IsVisible = true;
				entry.Text = file.SlotPosition;
			}
		}
	}
	
	private async void OnSaveSlotChangesClicked(object sender, EventArgs e)
	{
		try
		{
			// Validate only slots that are provided (empty is allowed)
			foreach (var file in SelectedProgramFiles)
			{
				var slot = file.SlotPosition?.Trim();
				if (string.IsNullOrEmpty(slot))
					continue;

				slot = slot.ToLowerInvariant();
				file.SlotPosition = slot;

				if (slot.Length != 1 || slot[0] < 'a' || slot[0] > 'z')
				{
					ShowError("Each slot should be a single letter from a to z (or leave it blank).");
					return;
				}
			}
			
			// Check for duplicate slots (ignore empty)
			var slots = SelectedProgramFiles
				.Select(f => f.SlotPosition?.Trim())
				.Where(s => !string.IsNullOrEmpty(s))
				.Select(s => s!.ToLowerInvariant())
				.ToList();

			var duplicates = slots
				.GroupBy(s => s)
				.Where(g => g.Count() > 1)
				.Select(g => g.Key)
				.ToList();
			
			if (duplicates.Any())
			{
				ShowError($"These slots are already in use: {string.Join(", ", duplicates)}. Please choose different ones.");
				return;
			}
			
			// Update slot positions in database (blank is allowed)
			foreach (var file in SelectedProgramFiles)
			{
				await _mongoService.UpdateProgrammingFileSlotAsync(file.Id, file.SlotPosition ?? string.Empty);
			}
			
			// await DisplayAlert("Success", "Slot positions updated successfully", "OK");
			await ShowSuccessMessage("Slot positions updated successfully");
			ErrorMessageBorder.IsVisible = false;

			// Re-sort list after slot updates so UI stays in a,b,c... order
			SortSelectedProgramFiles();
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't save your changes. Please try again.");
		}
	}

	private static int GetSlotSortKey(string? slot)
	{
		if (string.IsNullOrWhiteSpace(slot)) return int.MaxValue; // No slot goes last
		var c = char.ToLowerInvariant(slot.Trim()[0]);
		if (c < 'a' || c > 'z') return int.MaxValue;
		return c - 'a';
	}

	private void SortSelectedProgramFiles()
	{
		var sorted = SelectedProgramFiles
			.OrderBy(f => GetSlotSortKey(f.SlotPosition))
			.ThenBy(f => f.FileName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
			.ToList();

		SelectedProgramFiles.Clear();
		foreach (var file in sorted)
		{
			SelectedProgramFiles.Add(file);
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
				await NotificationService.ShowError("Please select a programming file to continue.");
				return;
			}

			// Validate that at least one playlist is selected
			var selectedPlaylists = _playlistItems.Where(p => p.IsChecked).ToList();
			if (!selectedPlaylists.Any())
			{
				await NotificationService.ShowError("Please select at least one playlist to continue.");
				return;
			}

			// Get current user
			var currentUser = await _authService.GetCurrentUserAsync();
			if (currentUser == null)
			{
				await NotificationService.ShowError("Please log in to continue.");
				return;
			}

			int processedCount = 0;

			foreach (var playlistItem in selectedPlaylists)
			{
				var project = playlistItem.Project;
				if (project == null)
					continue;

				// Prefer custom ProjectId when available (matches playlist ProjectId)
				var projectId = !string.IsNullOrEmpty(project.ProjectId) ? project.ProjectId : project.Id;

			// Get existing playlist files
				var existingPlaylistFiles = await _mongoService.GetProjectPlaylistsAsync(projectId);
			if (existingPlaylistFiles == null || existingPlaylistFiles.Count == 0)
			{
					await NotificationService.ShowWarning($"'{project.Title}' has no files and was skipped.");
					continue;
			}

			// Iterate through selected program files and update corresponding playlist files
			foreach (var progFile in SelectedProgramFiles)
			{
				if (string.IsNullOrEmpty(progFile.SlotPosition)) continue;

				// Find matching playlist files with the same slot letter
				var matchingPlaylistFiles = existingPlaylistFiles
					.Where(p => string.Equals(p.SlotPosition, progFile.SlotPosition, StringComparison.OrdinalIgnoreCase))
					.ToList();

				foreach (var match in matchingPlaylistFiles)
				{
					// Delete old playlist entry
					await _mongoService.DeletePlaylistByIdAsync(match.Id);

					// Create new playlist entry with program file info but keeping playlist metadata
					var newFileName = IO.Path.GetFileName(progFile.FilePath);
					var newPlaylistFile = new PlaylistModel
					{
							ProjectId = projectId,
						FileName = newFileName,
						FilePath = progFile.FilePath,
						SlotPosition = match.SlotPosition, // Keep original slot
						OrderIndex = match.OrderIndex, // Keep original order
						UserId = currentUser.Id,
						Notes = new List<string>(), // Reset notes
						IsPrivate = project.IsPrivate,
						CreatedAt = DateTime.UtcNow
					};

					await _mongoService.CreatePlaylistAsync(newPlaylistFile);
				}
			}

				processedCount++;
			}

			if (processedCount == 0)
			{
				await NotificationService.ShowWarning("No playlists were updated. Please check that they contain files.");
				return;
			}

			var successMessage = processedCount == 1
				? "Playlist processed successfully!"
				: $"{processedCount} playlists processed successfully!";
			await ShowSuccessMessage(successMessage);
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't apply those files. Please try again.");
		}
	}

	private async Task ShowSuccessMessage(string message)
	{
		await NotificationService.ShowSuccess(message);
	}

	private void UpdateApplyFilesButtonState()
	{
		if (ApplyFilesButton == null)
			return;

		int selectedCount = _playlistItems.Count(p => p.IsChecked);

		// Keep button text constant per UX request
		ApplyFilesButton.Text = "Apply Files";

		if (selectedCount > 0)
		{
			ApplyFilesButton.IsEnabled = true;
			ApplyFilesButton.BackgroundColor = Color.FromArgb("#28a745");
			ApplyFilesButton.Opacity = 1.0;
		}
		else
		{
			ApplyFilesButton.IsEnabled = false;
			ApplyFilesButton.BackgroundColor = Color.FromArgb("#6c757d");
			ApplyFilesButton.Opacity = 0.7;
		}
	}

	private class PlaylistSelectionItem
	{
		public bool IsChecked { get; set; }
		public Database.Models.ProjectModel Project { get; set; }
	}

	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//DashboardPage");
	}
}

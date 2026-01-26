using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Database;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.IO.Compression;
using Microsoft.Maui.Controls.Shapes;
using System.Linq;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;

using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class DownloadsPage : ContentPage
{
	private readonly CarrotMongoService _mongoService;
	private readonly IAuthService _authService;
	private readonly CarrotDownload.FFmpeg.Interfaces.IFFmpegService _ffmpegService;

	public DownloadsPage(
		CarrotMongoService mongoService, 
		IAuthService authService,
		CarrotDownload.FFmpeg.Interfaces.IFFmpegService ffmpegService)
	{
		InitializeComponent();
		_mongoService = mongoService;
		_authService = authService;
		_ffmpegService = ffmpegService;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadRenderedFiles();
	}

	private async Task LoadRenderedFiles()
	{
		try
		{
			// Get current user
			var currentUser = await _authService.GetCurrentUserAsync();
			if (currentUser == null) return;

			// Get export history instead of raw projects
			var exportHistory = (await _mongoService.GetUserExportHistoryAsync(currentUser.Id))
				.Where(e => !e.ZipFileName.ToLower().EndsWith(".zip"))
				.ToList();

			RenderedFilesContainer.Children.Clear();

			if (!exportHistory.Any())
			{
				// Show empty state message
				RenderedFilesContainer.Children.Add(new Label
				{
					Text = "No rendered files yet. Create one from the Sequence page!",
					FontSize = 16,
					TextColor = Color.FromArgb("#666"),
					HorizontalOptions = LayoutOptions.Center,
					Margin = new Thickness(0, 50)
				});
				return;
			}

			foreach (var export in exportHistory)
			{
				CreateCollapsibleFileCard(export);
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't load your downloads. Please try again.");
		}
	}

	private void CreateCollapsibleFileCard(Database.Models.ExportHistoryModel export)
	{
		// Main Border (Card)
		var mainBorder = new Border
		{
			Stroke = Color.FromArgb("#FF6600"),
			StrokeThickness = 1.5,
			BackgroundColor = Colors.White,
			Padding = new Thickness(15, 10), // Tighter padding
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Margin = new Thickness(0, 0, 0, 10) // Reduced gap between cards
		};

		var mainLayout = new VerticalStackLayout { Spacing = 5 };

		// Header (always visible)
		var headerLayout = new Grid
		{
			ColumnDefinitions = new ColumnDefinitionCollection 
			{ 
				new ColumnDefinition { Width = GridLength.Star },
				new ColumnDefinition { Width = GridLength.Auto } 
			},
			HeightRequest = 35 // Slimmer header
		};

		var titleLabel = new Label
		{
			Text = $"{export.ZipFileName}",
			FontSize = 14, // Smaller title
			FontAttributes = FontAttributes.Bold,
			TextColor = Color.FromArgb("#FF6600"),
			VerticalOptions = LayoutOptions.Center
		};

		var arrowLabel = new Label
		{
			Text = "▼",
			FontSize = 18,
			TextColor = Color.FromArgb("#FF6600"),
			VerticalOptions = LayoutOptions.Center
		};

		headerLayout.Children.Add(titleLabel);
		Grid.SetColumn(titleLabel, 0);
		headerLayout.Children.Add(arrowLabel);
		Grid.SetColumn(arrowLabel, 1);

		// Expandable Content
		var contentLayout = new VerticalStackLayout
		{
			Spacing = 10, // Tighter spacing
			IsVisible = false,
			Padding = new Thickness(5, 10, 5, 5)
		};

		// Project Name Header
		var projectNameLabel = new Label
		{
			Text = export.ProjectTitles != null && export.ProjectTitles.Any() ? export.ProjectTitles[0] : "Rendered File",
			FontSize = 24, // Smaller project title
			FontAttributes = FontAttributes.Bold,
			TextColor = Colors.Black,
			Margin = new Thickness(0, 0, 0, 5)
		};
		contentLayout.Children.Add(projectNameLabel);

		// Metadata Section - Tighter
		var resolutionLabel = new Label { FontSize = 13, TextColor = Colors.Black, Margin = new Thickness(0, 1) };
		var durationLabel = new Label { FontSize = 13, TextColor = Colors.Black, Margin = new Thickness(0, 1) };
		var framerateLabel = new Label { FontSize = 13, TextColor = Colors.Black, Margin = new Thickness(0, 1) };
		var fileSizeLabel = new Label { FontSize = 13, TextColor = Colors.Black, Margin = new Thickness(0, 1) };
		var exportedDateLabel = new Label { FontSize = 13, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 5, 0, 0) };

		// Set exported date immediately
		exportedDateLabel.FormattedText = new FormattedString();
		exportedDateLabel.FormattedText.Spans.Add(new Span { Text = "Exported: ", FontAttributes = FontAttributes.Bold });
		exportedDateLabel.FormattedText.Spans.Add(new Span { Text = export.ExportedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") });

		contentLayout.Children.Add(resolutionLabel);
		contentLayout.Children.Add(durationLabel);
		contentLayout.Children.Add(framerateLabel);
		contentLayout.Children.Add(fileSizeLabel);
		contentLayout.Children.Add(exportedDateLabel);

		// Video Preview Area - Smaller
		var previewContainer = new Border
		{
			HeightRequest = 180, // Smaller preview
			WidthRequest = 320,
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			BackgroundColor = Color.FromArgb("#1a1a1a"),
			Margin = new Thickness(0, 8),
			HorizontalOptions = LayoutOptions.Start,
			IsVisible = false 
		};

		var previewImage = new Image
		{
			Aspect = Aspect.AspectFill,
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.Fill
		};

		var playIcon = new Label
		{
			Text = "▶",
			FontSize = 40,
			TextColor = Colors.White,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Opacity = 0.8
		};

		var previewGrid = new Grid();
		previewGrid.Children.Add(previewImage);
		previewGrid.Children.Add(playIcon);
		previewContainer.Content = previewGrid;

		var previewTap = new TapGestureRecognizer();
		previewTap.Tapped += (s, e) => OnPlayVideoClicked(export.ZipFilePath);
		previewContainer.GestureRecognizers.Add(previewTap);

		contentLayout.Children.Add(previewContainer);

		// Fetch Metadata and Thumbnail
		_ = Task.Run(async () =>
		{
			try
			{
				if (File.Exists(export.ZipFilePath))
				{
					var info = await _ffmpegService.GetMediaInfoAsync(export.ZipFilePath);
					var thumbPath = System.IO.Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.jpg");
					var generatedPath = await _ffmpegService.GenerateThumbnailAsync(export.ZipFilePath, thumbPath, TimeSpan.FromSeconds(1));
					
					// Get file size
					var fileInfo = new FileInfo(export.ZipFilePath);
					string fileSizeText = fileInfo.Length < 1024 * 1024 
						? $"{fileInfo.Length / 1024.0:F1} KB" 
						: $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB";

					MainThread.BeginInvokeOnMainThread(() =>
					{
						resolutionLabel.FormattedText = new FormattedString();
						resolutionLabel.FormattedText.Spans.Add(new Span { Text = "Resolution: ", FontAttributes = FontAttributes.Bold });
						resolutionLabel.FormattedText.Spans.Add(new Span { Text = $"{info.Width}x{info.Height}" });

						// Format duration as HH:MM:SS or MM:SS
						var duration = info.Duration;
						string durationText = duration.TotalHours >= 1 
							? $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}" 
							: $"{duration.Minutes:D2}:{duration.Seconds:D2}";
						durationLabel.FormattedText = new FormattedString();
						durationLabel.FormattedText.Spans.Add(new Span { Text = "Duration: ", FontAttributes = FontAttributes.Bold });
						durationLabel.FormattedText.Spans.Add(new Span { Text = durationText });

						// Framerate is already a double
						framerateLabel.FormattedText = new FormattedString();
						framerateLabel.FormattedText.Spans.Add(new Span { Text = "Framerate: ", FontAttributes = FontAttributes.Bold });
						framerateLabel.FormattedText.Spans.Add(new Span { Text = $"{info.FrameRate:F2} fps" });

						fileSizeLabel.FormattedText = new FormattedString();
						fileSizeLabel.FormattedText.Spans.Add(new Span { Text = "File Size: ", FontAttributes = FontAttributes.Bold });
						fileSizeLabel.FormattedText.Spans.Add(new Span { Text = fileSizeText });

						if (!string.IsNullOrEmpty(generatedPath) && File.Exists(generatedPath))
						{
							previewImage.Source = ImageSource.FromFile(generatedPath);
							previewContainer.IsVisible = true;
						}
					});
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error loading media details: {ex.Message}");
			}
		});

		// Button Container
		var buttonContainer = new HorizontalStackLayout
		{
			Spacing = 10,
			HorizontalOptions = LayoutOptions.Center,
			Margin = new Thickness(0, 10, 0, 5)
		};

		// Play Button
		var playButton = new Button
		{
			Text = "▶ Play",
			BackgroundColor = Color.FromArgb("#28a745"),
			TextColor = Colors.White,
			FontSize = 14,
			FontAttributes = FontAttributes.Bold,
			HeightRequest = 40,
			WidthRequest = 120,
			CornerRadius = 6
		};
		playButton.Clicked += async (s, e) => await OnPlayVideoClicked(export.ZipFilePath);

		// Download Button - Smaller/Orange
		var downloadButton = new Button
		{
			Text = "Download",
			BackgroundColor = Color.FromArgb("#FF6600"),
			TextColor = Colors.White,
			FontSize = 14,
			FontAttributes = FontAttributes.Bold,
			HeightRequest = 40,
			WidthRequest = 120,
			CornerRadius = 6
		};
		downloadButton.Clicked += async (s, e) => await OnDownloadVideoClicked(export.ZipFilePath);

		// Delete Button
		var deleteButton = new Button
		{
			Text = "Delete",
			BackgroundColor = Color.FromArgb("#dc3545"),
			TextColor = Colors.White,
			FontSize = 14,
			FontAttributes = FontAttributes.Bold,
			HeightRequest = 40,
			WidthRequest = 120,
			CornerRadius = 6
		};
		deleteButton.Clicked += async (s, e) => await OnDeleteVideoClicked(export);

		buttonContainer.Children.Add(playButton);
		buttonContainer.Children.Add(downloadButton);
		buttonContainer.Children.Add(deleteButton);

		contentLayout.Children.Add(buttonContainer);

		// Assemble Card
		mainLayout.Children.Add(headerLayout);
		mainLayout.Children.Add(contentLayout);
		mainBorder.Content = mainLayout;

		// Toggle expand/collapse
		var tapGesture = new TapGestureRecognizer();
		tapGesture.Tapped += (s, e) =>
		{
			contentLayout.IsVisible = !contentLayout.IsVisible;
			arrowLabel.Text = contentLayout.IsVisible ? "▲" : "▼";
		};
		headerLayout.GestureRecognizers.Add(tapGesture);

		RenderedFilesContainer.Children.Add(mainBorder);
	}



	private async Task OnPlayVideoClicked(string filePath)
	{
		try
		{
			if (File.Exists(filePath))
			{
				await Launcher.Default.OpenAsync(new OpenFileRequest
				{
					File = new ReadOnlyFile(filePath)
				});
			}
			else
			{
				await NotificationService.ShowError("We couldn't find that file. It may have been moved or deleted.");
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't play that file. Please check that it exists.");
		}
	}

	private async Task OnDownloadVideoClicked(string filePath)
	{
		try
		{
			if (!File.Exists(filePath))
			{
				await NotificationService.ShowError("We couldn't find that file. It may have been moved or deleted.");
				return;
			}

			string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			string downloadsPath = System.IO.Path.Combine(userProfile, "Downloads");
			string fileName = System.IO.Path.GetFileName(filePath);
			string destPath = System.IO.Path.Combine(downloadsPath, fileName);

			// Handle duplicates
			int count = 1;
			while (File.Exists(destPath))
			{
				string nameOnly = System.IO.Path.GetFileNameWithoutExtension(fileName);
				string ext = System.IO.Path.GetExtension(fileName);
				destPath = System.IO.Path.Combine(downloadsPath, $"{nameOnly}_{count}{ext}");
				count++;
			}

			File.Copy(filePath, destPath);
			await NotificationService.ShowSuccess($"Success! Your file has been saved to:\n{destPath}");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't download that file. Please try again.");
		}
	}

	private async Task OnDeleteVideoClicked(Database.Models.ExportHistoryModel export)
	{
		try
		{
			// Confirm deletion
			var dialog = new ConfirmationDialog("Delete Render", 
				$"Are you sure you want to delete '{export.ZipFileName}'?\n\nThis will remove it from your downloads list and delete the file from disk.",
				"Delete", "Cancel");
			await Navigation.PushModalAsync(dialog);
			
			if (!await dialog.GetResultAsync()) return;

			// Delete from database
			await _mongoService.DeleteExportHistoryAsync(export.Id);

			// NOTE: Rendered/exported files (MP4/ZIP) created by the app can be deleted
			// These are app-generated files, not user's original files
			// User can manually delete them from Downloads folder if desired
			if (File.Exists(export.ZipFilePath))
			{
				try
				{
					File.Delete(export.ZipFilePath);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Warning: Could not delete rendered file: {ex.Message}");
				}
			}

			// Reload the list
			await LoadRenderedFiles();
			await NotificationService.ShowSuccess("The render has been deleted successfully!");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError("We couldn't delete that render. Please try again.");
		}
	}
	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//DashboardPage");
	}
}

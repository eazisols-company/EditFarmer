using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Database;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.IO.Compression;
using Microsoft.Maui.Controls.Shapes;
using System.Linq;

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
			await NotificationService.ShowError($"Failed to load downloads: {ex.Message}");
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

		contentLayout.Children.Add(resolutionLabel);
		contentLayout.Children.Add(durationLabel);
		contentLayout.Children.Add(framerateLabel);

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

					MainThread.BeginInvokeOnMainThread(() =>
					{
						resolutionLabel.FormattedText = new FormattedString();
						resolutionLabel.FormattedText.Spans.Add(new Span { Text = "Resolution: ", FontAttributes = FontAttributes.Bold });
						resolutionLabel.FormattedText.Spans.Add(new Span { Text = $"{info.Width}x{info.Height}" });

						durationLabel.FormattedText = new FormattedString();
						durationLabel.FormattedText.Spans.Add(new Span { Text = "Duration: ", FontAttributes = FontAttributes.Bold });
						durationLabel.FormattedText.Spans.Add(new Span { Text = $"{info.Duration.TotalSeconds:F6}" });

						framerateLabel.FormattedText = new FormattedString();
						framerateLabel.FormattedText.Spans.Add(new Span { Text = "Framerate: ", FontAttributes = FontAttributes.Bold });
						framerateLabel.FormattedText.Spans.Add(new Span { Text = $"{info.FrameRate}" });

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

		// Download Button - Smaller/Orange
		var downloadButton = new Button
		{
			Text = "Download",
			BackgroundColor = Color.FromArgb("#FF6600"),
			TextColor = Colors.White,
			FontSize = 14,
			FontAttributes = FontAttributes.Bold,
			HeightRequest = 40,
			WidthRequest = 200,
			CornerRadius = 6,
			HorizontalOptions = LayoutOptions.Center,
			Margin = new Thickness(0, 10, 0, 5)
		};
		downloadButton.Clicked += async (s, e) => await OnDownloadVideoClicked(export.ZipFilePath);

		contentLayout.Children.Add(downloadButton);

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



	private async void OnPlayVideoClicked(string filePath)
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
				await NotificationService.ShowError("Processed file not found at the saved location.");
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Could not play file: {ex.Message}");
		}
	}

	private async Task OnDownloadVideoClicked(string filePath)
	{
		try
		{
			if (!File.Exists(filePath))
			{
				await NotificationService.ShowError("Processed file not found at the saved location.");
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
			await NotificationService.ShowSuccess($"File downloaded to:\n{destPath}");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to download: {ex.Message}");
		}
	}
	private async void OnBackClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//DashboardPage");
	}
}

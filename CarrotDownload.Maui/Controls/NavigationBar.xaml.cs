using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Maui.Views;
using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Controls;

public partial class NavigationBar : ContentView
{
	private readonly IAuthService _authService;
	private string _currentAccentColor = "#ffffff";

	public NavigationBar()
	{
		InitializeComponent();
		_authService = Application.Current!.Handler!.MauiContext!.Services.GetService<IAuthService>()!;
		LoadUserInfo();
		LoadAccentColor();
		SubscribeToAccentColorChanges();
        SubscribeToUserInfoChanges();
		
		// Subscribe to Shell navigation events
		if (Shell.Current != null)
		{
			Shell.Current.Navigated += OnShellNavigated;
		}
	}

	private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		// Update highlighting when navigation completes
		HighlightCurrentPage();
	}

	protected override void OnParentSet()
	{
		base.OnParentSet();
		// Delay highlighting to ensure Shell is ready
		Dispatcher.Dispatch(() => HighlightCurrentPage());
	}

	private async void LoadUserInfo()
	{
		var user = await _authService.GetCurrentUserAsync();
		if (user != null)
		{
			WelcomeLabel.Text = $"Welcome {user.FullName}";
			// Load user-specific accent color
			LoadAccentColor(user.Id);
		}
		else
		{
			// Load default/global accent color
			LoadAccentColor(null);
		}
	}

	private void LoadAccentColor(string? userId = null)
	{
		// Load accent color from in-memory session (defaults to white per app launch)
		_currentAccentColor = AccentColorSession.CurrentColor;
		ApplyAccentColor(_currentAccentColor);
	}

	private void SubscribeToAccentColorChanges()
	{
		// Subscribe to accent color change messages
		MessagingCenter.Subscribe<SettingsPage, string>(this, "AccentColorChanged", (sender, color) =>
		{
			_currentAccentColor = color;
			ApplyAccentColor(color);
			HighlightCurrentPage(); // Re-apply highlighting with new color
		});
	}

    private void SubscribeToUserInfoChanges()
    {
        _authService.UserInfoUpdated += (sender, newName) =>
        {
            Dispatcher.Dispatch(() => WelcomeLabel.Text = $"Welcome {newName}");
        };
    }

	public void ApplyAccentColor(string colorHex)
	{
		try
		{
			AccentBorder.BackgroundColor = Color.Parse(colorHex);
			// WelcomeLabel.TextColor is now static #FF6900
		}
		catch
		{
			// Fallback to default color if parsing fails
			AccentBorder.BackgroundColor = Color.Parse("#ff5722");
		}
	}

	private void HighlightCurrentPage()
	{
		// Reset all labels to default
		ResetAllLabels();

		// Get current route from Shell
		if (Shell.Current?.CurrentState?.Location != null)
		{
			var currentRoute = Shell.Current.CurrentState.Location.OriginalString;
			var accentColor = Color.Parse(_currentAccentColor);

			// Highlight the corresponding label based on route
			if (currentRoute.Contains("DashboardPage", StringComparison.OrdinalIgnoreCase))
				HighlightLabel(HomeLabel, accentColor);
			else if (currentRoute.Contains("SettingsPage", StringComparison.OrdinalIgnoreCase))
				HighlightLabel(SettingsLabel, accentColor);
		}
	}

	private void ResetAllLabels()
	{
		var labels = new[] { HomeLabel, SettingsLabel, LogoutLabel };
		foreach (var label in labels)
		{
			label.TextColor = Color.Parse("#333");
			label.FontAttributes = FontAttributes.None;
		}
	}

	private void HighlightLabel(Label label, Color accentColor)
	{
        // If accent color is white (or very light), use black for text visibility
        // otherwise use the accent color
        if (_currentAccentColor.Equals("#ffffff", StringComparison.OrdinalIgnoreCase) || 
            _currentAccentColor.ToLower().Contains("white"))
        {
            label.TextColor = Colors.Black;
        }
        else
        {
		    label.TextColor = accentColor;
        }
		label.FontAttributes = FontAttributes.Bold;
	}

	private async void OnHomeClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//DashboardPage");
		HighlightCurrentPage();
	}

	private async void OnSettingsClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//SettingsPage");
		HighlightCurrentPage();
	}

	private async void OnLogoutClicked(object sender, EventArgs e)
	{
		var confirm = await Application.Current!.MainPage!.DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
		if (confirm)
		{
			await _authService.LogoutAsync();
			Application.Current!.MainPage = new AppShell();
		}
	}
}

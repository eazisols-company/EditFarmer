using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Auth.Models;
using CarrotDownload.Maui.Services;
using System.Runtime.InteropServices;

namespace CarrotDownload.Maui.Views;

public partial class LoginPage : ContentPage
{
	private readonly IAuthService _authService;
	private readonly CarrotDownload.Database.CarrotMongoService _mongoService;
	private readonly IDeviceInfoService _deviceInfoService;
	private bool isPasswordVisible = false;

	public LoginPage(IAuthService authService, CarrotDownload.Database.CarrotMongoService mongoService, IDeviceInfoService deviceInfoService)
	{
		InitializeComponent();
		_authService = authService;
		_mongoService = mongoService;
		_deviceInfoService = deviceInfoService;
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Block non-windows usage
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MacOverlay.IsVisible = true;
            return;
        }

    }

	private async void OnPasswordToggleTapped(object sender, EventArgs e)
	{
		isPasswordVisible = !isPasswordVisible;
		PasswordEntry.IsPassword = !isPasswordVisible;
		PasswordToggle.Text = isPasswordVisible ? "👁‍🗨" : "👁";
		
		// Wait for platform to update IsPassword state
		await Task.Delay(50);
		
		PasswordEntry.Focus();
		if (PasswordEntry.Text != null)
		{
			PasswordEntry.CursorPosition = PasswordEntry.Text.Length;
		}
	}

	private async void ShowMessage(string message, bool isSuccess)
	{
		if (isSuccess)
			await NotificationService.ShowSuccess(message);
		else
			await NotificationService.ShowError(message);
	}

    private async void OnLoginClicked(object sender, EventArgs e)
    {
		// Disable button and show loading
		LoginButton.IsEnabled = false;
		var originalText = LoginButton.Text;
		LoginButton.Text = "Logging in...";

		var email = EmailEntry.Text?.Trim();
		var password = PasswordEntry.Text;

			// Validation
		if (string.IsNullOrEmpty(email))
		{
			ShowMessage("Please enter your email address", false);
			LoginButton.IsEnabled = true;
			LoginButton.Text = originalText;
			return;
		}

		if (string.IsNullOrEmpty(password))
		{
			ShowMessage("Please enter your password", false);
			LoginButton.IsEnabled = true;
			LoginButton.Text = originalText;
			return;
		}

		try
		{
			// Get Device ID (MAC Address)
			var deviceId = _deviceInfoService.GetDeviceId();
			
			// Authenticate user with device binding check
			var user = await _mongoService.LoginUserAsync(email, password, deviceId);

			if (user == null)
			{
				ShowMessage("Invalid credentials or device not authorized", false);
				LoginButton.IsEnabled = true;
				LoginButton.Text = originalText;
				return;
			}

			// Store user data in secure storage
			var userData = new UserData
			{
				Id = user.Id ?? "",
				FullName = user.FullName ?? "User",
				Email = user.Email ?? email,
				Role = user.Role ?? "User",
				IsActive = user.IsActive,
				CreatedAt = user.CreatedAt
			};

			var mockToken = "token-" + Guid.NewGuid().ToString("N");
			await SecureStorage.Default.SetAsync("auth_token", mockToken);
			await SecureStorage.Default.SetAsync("user_data", System.Text.Json.JsonSerializer.Serialize(userData));
			await SecureStorage.Default.SetAsync("is_admin", "false"); // Ensure admin flag is cleared

			ShowMessage("Login successful! Redirecting...", true);
			
			// Wait a moment to show success message
			await Task.Delay(1000);
			
			// Navigate to dashboard - Handle case where MainPage is not AppShell (e.g. after Admin Logout)
			if (Shell.Current != null)
			{
				await Shell.Current.GoToAsync("//DashboardPage");
			}
			else
			{
				// Reset to AppShell if we're in a regular NavigationPage
				Application.Current.MainPage = new AppShell();
				await Shell.Current.GoToAsync("//DashboardPage");
			}
		}
		catch (Exception ex)
		{
			ShowMessage($"Error: {ex.Message}", false);
			LoginButton.IsEnabled = true;
			LoginButton.Text = originalText;
			ShowMessage($"Error: {ex.Message}", false);
			LoginButton.IsEnabled = true;
			LoginButton.Text = originalText;
		}
    }

    private async void OnAdminLoginClicked(object sender, EventArgs e)
    {
        try
        {
            var email = await DisplayPromptAsync("Admin Login", "Enter admin email:", "Login", "Cancel", placeholder: "admin@carrot.com");
            if (string.IsNullOrEmpty(email))
                return;

            var password = await DisplayPromptAsync("Admin Login", "Enter admin password:", "Login", "Cancel", placeholder: "Password");
            if (string.IsNullOrEmpty(password))
                return;

            // Verify admin credentials in database
            // Verify admin credentials in database
            var isValidAdmin = await _mongoService.LoginAdminInDbAsync(email, password);
                        
            if (isValidAdmin)
            {
                await SecureStorage.Default.SetAsync("is_admin", "true");
                
                try
                {
                    var adminDashboard = new AdminDashboardPage(_mongoService);
                    
                    var adminNavPage = new NavigationPage(adminDashboard)
                    {
                        BarBackgroundColor = Colors.Transparent,
                        BarTextColor = Colors.Transparent
                    };
                    
                    // Hide the navigation bar completely
                    NavigationPage.SetHasNavigationBar(adminDashboard, false);
                    
                    MainThread.BeginInvokeOnMainThread(() => {
                        try {
                            Application.Current.MainPage = adminNavPage;
                        } catch (Exception ex) {
                        }
                    });
                }
                catch (Exception navEx)
                {
                    await NotificationService.ShowError($"Failed to initialize admin portal: {navEx.Message}");
                }
            }
            else
            {
                await NotificationService.ShowError("Invalid admin credentials");
            }
        }
        catch (Exception ex)
        {
            await NotificationService.ShowError($"Admin login failed: {ex.Message}");
        }
    }
}

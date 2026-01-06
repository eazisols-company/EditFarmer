using CarrotDownload.Database.Models;
using CarrotDownload.Maui.Services;
using CarrotDownload.Auth.Helpers;

namespace CarrotDownload.Maui.Views;

public partial class AdminUserManagementPage : ContentPage
{
    private readonly CarrotDownload.Database.CarrotMongoService _mongoService;

    public AdminUserManagementPage(CarrotDownload.Database.CarrotMongoService mongoService)
    {
        InitializeComponent();
        _mongoService = mongoService;
        
        // Wire up navigation events
        AdminNavBar.DashboardClicked += OnDashboardNavClicked;
        AdminNavBar.UsersClicked += (s, e) => { /* Already on Users */ };
        AdminNavBar.MacIdsClicked += OnMacIdsNavClicked;
        AdminNavBar.LogoutClicked += OnLogoutNavClicked;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Set active page every time the page appears
        AdminNavBar.SetActivePage("Users");
        
        await LoadUsersAsync();
    }
    
    private async void OnDashboardNavClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnMacIdsNavClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AdminMacIdManagementPage(_mongoService));
    }

    private async void OnLogoutNavClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Confirm", "Are you sure you want to logout?", "Yes", "No");
        if (confirm)
        {
            await SecureStorage.Default.SetAsync("is_admin", "false");
            Application.Current.MainPage = new AppShell();
        }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            UsersContainer.Children.Clear();

            var users = await _mongoService.GetAllUsersAsync();

            if (users.Count == 0)
            {
                UsersContainer.Children.Add(new Label
                {
                    Text = "No users found",
                    TextColor = Colors.Gray,
                    FontSize = 16,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 50, 0, 0)
                });
                return;
            }

            foreach (var user in users)
            {
                var userCard = CreateUserCard(user);
                UsersContainer.Children.Add(userCard);
            }
        }
        catch (Exception ex)
        {
            await NotificationService.ShowError($"Failed to load users: {ex.Message}");
        }
    }

    private Frame CreateUserCard(UserModel user)
    {
        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = user.IsBanned ? Color.FromArgb("#dc3545") : Color.FromArgb("#e5e5e5"),
            CornerRadius = 8,
            Padding = 15,
            Margin = new Thickness(0, 0, 0, 10),
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.08f, Radius = 8, Offset = new Point(0, 2) }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 8
        };

        // Name
        var nameLabel = new Label
        {
            Text = $"👤 {user.FullName}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
        grid.Add(nameLabel, 0, 0);

        // Email
        var emailLabel = new Label
        {
            Text = $"📧 {user.Email}",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        };
        grid.Add(emailLabel, 0, 1);

        // Status
        var statusText = user.IsBanned ? "🚫 BANNED" : (user.IsWhitelisted ? "⭐ WHITELISTED" : "✅ Active");
        var statusColor = user.IsBanned ? "#dc3545" : (user.IsWhitelisted ? "#ff5722" : "#28a745");
        var statusLabel = new Label
        {
            Text = statusText,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(statusColor)
        };
        grid.Add(statusLabel, 0, 2);

        // Last Login
        var lastLogin = user.LastLoginAt.HasValue 
            ? user.LastLoginAt.Value.ToString("MMM dd, yyyy HH:mm") 
            : "Never";
        var lastLoginLabel = new Label
        {
            Text = $"🕒 Last Login: {lastLogin}",
            FontSize = 12,
            TextColor = Color.FromArgb("#999")
        };
        grid.Add(lastLoginLabel, 0, 3);

        // Action Buttons
        var buttonStack = new VerticalStackLayout
        {
            Spacing = 8
        };

        var banButton = new Button
        {
            Text = user.IsBanned ? "✅ Unban" : "🚫 Ban",
            BackgroundColor = user.IsBanned ? Color.FromArgb("#28a745") : Color.FromArgb("#dc3545"),
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 6,
            HeightRequest = 35
        };
        banButton.Clicked += async (s, e) => await OnBanUnbanClicked(user);
        buttonStack.Add(banButton);

        var whitelistButton = new Button
        {
            Text = user.IsWhitelisted ? "⭐ Remove Whitelist" : "⭐ Whitelist",
            BackgroundColor = Color.FromArgb("#FFE66D"),
            TextColor = Colors.Black,
            FontSize = 12,
            CornerRadius = 6,
            HeightRequest = 35
        };
        whitelistButton.Clicked += async (s, e) => await OnWhitelistClicked(user);
        buttonStack.Add(whitelistButton);

        var resetPasswordButton = new Button
        {
            Text = "🔑 Reset Password",
            BackgroundColor = Color.FromArgb("#4ECDC4"),
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 6,
            HeightRequest = 35
        };
        resetPasswordButton.Clicked += async (s, e) => await OnResetPasswordClicked(user);
        buttonStack.Add(resetPasswordButton);

        var deleteButton = new Button
        {
            Text = "🗑️ Delete User",
            BackgroundColor = Color.FromArgb("#dc3545"), // Red color
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 6,
            HeightRequest = 35
        };
        deleteButton.Clicked += async (s, e) => await OnDeleteUserClicked(user);
        buttonStack.Add(deleteButton);

        grid.Add(buttonStack, 1, 0);
        Grid.SetRowSpan(buttonStack, 4);

        frame.Content = grid;
        return frame;
    }

    private async Task OnDeleteUserClicked(UserModel user)
    {
        var confirm = await DisplayAlert("Delete User", 
            $"Are you sure you want to PERMANENTLY delete user {user.FullName} ({user.Email})?\n\nThis action cannot be undone.", 
            "Delete", 
            "Cancel");
        
        if (confirm)
        {
            try
            {
                var success = await _mongoService.DeleteUserAsync(user.Id);

                if (success)
                {
                    await NotificationService.ShowSuccess("User deleted successfully!");
                    await LoadUsersAsync();
                }
                else
                {
                    await NotificationService.ShowError("Failed to delete user");
                }
            }
            catch (Exception ex)
            {
                await NotificationService.ShowError($"Error deleting user: {ex.Message}");
            }
        }
    }

    private async Task OnBanUnbanClicked(UserModel user)
    {
        var action = user.IsBanned ? "unban" : "ban";
        var confirm = await DisplayAlert("Confirm", $"Are you sure you want to {action} {user.FullName}?", "Yes", "No");
        
        if (confirm)
        {
            try
            {
                bool success;
                if (user.IsBanned)
                {
                    success = await _mongoService.UnbanUserAsync(user.Id);
                }
                else
                {
                    success = await _mongoService.BanUserAsync(user.Id);
                }

                if (success)
                {
                    await NotificationService.ShowSuccess($"User {action}ned successfully!");
                    await LoadUsersAsync();
                }
                else
                {
                    await NotificationService.ShowError("Failed to update user status");
                }
            }
            catch (Exception ex)
            {
                await NotificationService.ShowError(ex.Message);
            }
        }
    }

    private async Task OnWhitelistClicked(UserModel user)
    {
        var action = user.IsWhitelisted ? "remove from whitelist" : "add to whitelist";
        var confirm = await DisplayAlert("Confirm", $"Are you sure you want to {action} {user.FullName}?", "Yes", "No");
        
        if (confirm)
        {
            try
            {
                var success = await _mongoService.WhitelistUserAsync(user.Id, !user.IsWhitelisted);

                if (success)
                {
                    await NotificationService.ShowSuccess("Whitelist status updated!");
                    await LoadUsersAsync();
                }
                else
                {
                    await NotificationService.ShowError("Failed to update whitelist status");
                }
            }
            catch (Exception ex)
            {
                await NotificationService.ShowError(ex.Message);
            }
        }
    }

    private async Task OnResetPasswordClicked(UserModel user)
    {
        // Don't ask for current password in admin mode - admins should be able to force reset
        var confirm = await DisplayAlert("Confirm", $"Reset password for user {user.FullName}?", "Yes", "No");
             
        if (!confirm) return;

        try
        {
            var newPassword = await DisplayPromptAsync("Reset Password", 
                $"Enter new password for {user.FullName}:", 
                "Reset", 
                "Cancel",
                placeholder: "New password");

            if (string.IsNullOrEmpty(newPassword)) return;

            // Validate new password requirements
            var validation = PasswordValidator.Validate(newPassword);
            if (!validation.IsValid)
            {
                await NotificationService.ShowError(validation.Message);
                return;
            }

            if (!string.IsNullOrEmpty(newPassword))
            {
                var success = await _mongoService.ResetUserPasswordAsync(user.Id, newPassword);

                if (success)
                {
                    await NotificationService.ShowSuccess($"Password reset successfully!\nNew password: {newPassword}");
                }
                else
                {
                    await NotificationService.ShowError("Failed to reset password");
                }
            }
        }
        catch (Exception ex)
        {
            await NotificationService.ShowError(ex.Message);
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadUsersAsync();
    }
}

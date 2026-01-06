using CarrotDownload.Database.Models;
using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class AdminMacIdManagementPage : ContentPage
{
    private readonly CarrotDownload.Database.CarrotMongoService _mongoService;

    public AdminMacIdManagementPage(CarrotDownload.Database.CarrotMongoService mongoService)
    {
        InitializeComponent();
        _mongoService = mongoService;
        
        // Wire up navigation events
        AdminNavBar.DashboardClicked += OnDashboardNavClicked;
        AdminNavBar.UsersClicked += OnUsersNavClicked;
        AdminNavBar.MacIdsClicked += (s, e) => { /* Already on MAC IDs */ };
        AdminNavBar.LogoutClicked += OnLogoutNavClicked;
    }
    
    private async void OnDashboardNavClicked(object sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
    }

    private async void OnUsersNavClicked(object sender, EventArgs e)
    {
        // Check if the previous page in the stack is AdminUserManagementPage
        var stack = Navigation.NavigationStack;
        if (stack.Count > 1 && stack[stack.Count - 2] is AdminUserManagementPage)
        {
            await Navigation.PopAsync();
        }
        else
        {
            await Navigation.PushAsync(new AdminUserManagementPage(_mongoService));
        }
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Set active page every time the page appears
        AdminNavBar.SetActivePage("MacIds");
        
        await LoadMacIdsAsync();
    }

    private async Task LoadMacIdsAsync()
    {
        try
        {
            MacIdsContainer.Children.Clear();

            var macIdLogs = await _mongoService.GetAllMacIdLogsAsync();

            if (macIdLogs.Count == 0)
            {
                MacIdsContainer.Children.Add(new Label
                {
                    Text = "No MAC IDs found",
                    TextColor = Colors.Gray,
                    FontSize = 16,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 50, 0, 0)
                });
                return;
            }

            foreach (var log in macIdLogs)
            {
                var macCard = CreateMacIdCard(log);
                MacIdsContainer.Children.Add(macCard);
            }
        }
        catch (Exception ex)
        {
            await NotificationService.ShowError($"Failed to load MAC IDs: {ex.Message}");
        }
    }

    private Frame CreateMacIdCard(MacIdLogModel log)
    {
        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = log.IsBanned ? Color.FromArgb("#dc3545") : Color.FromArgb("#e5e5e5"),
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
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 8
        };

        // MAC ID
        var macIdLabel = new Label
        {
            Text = $"🔒 {log.MacId}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
        grid.Add(macIdLabel, 0, 0);

        // User Name
        var userNameLabel = new Label
        {
            Text = $"👤 {log.UserName}",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        };
        grid.Add(userNameLabel, 0, 1);

        // Email
        var emailLabel = new Label
        {
            Text = $"📧 {log.UserEmail}",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        };
        grid.Add(emailLabel, 0, 2);

        // Last Login
        var lastLoginLabel = new Label
        {
            Text = $"🕒 Last Login: {log.LastUsedAt.ToString("MMM dd, yyyy HH:mm")}",
            FontSize = 12,
            TextColor = Color.FromArgb("#999")
        };
        grid.Add(lastLoginLabel, 0, 3);

        // Login Count & Status
        var statusText = log.IsBanned ? "🚫 BANNED" : $"✅ Active ({log.LoginCount} logins)";
        var statusColor = log.IsBanned ? "#dc3545" : "#28a745";
        var statusLabel = new Label
        {
            Text = statusText,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(statusColor)
        };
        grid.Add(statusLabel, 0, 4);

        // Action Buttons Container
        var actionsStack = new VerticalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center
        };

        // Ban/Unban Button
        var banButton = new Button
        {
            Text = log.IsBanned ? "✅ Unban" : "🚫 Ban",
            BackgroundColor = log.IsBanned ? Color.FromArgb("#28a745") : Color.FromArgb("#dc3545"),
            TextColor = Colors.White,
            FontSize = 13,
            CornerRadius = 6,
            HeightRequest = 40,
            WidthRequest = 100
        };
        banButton.Clicked += async (s, e) => await OnBanUnbanMacIdClicked(log);
        actionsStack.Add(banButton);

        var deleteButton = new Button
        {
            Text = "🗑️ Delete",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#dc3545"), // Red text
            BorderColor = Color.FromArgb("#dc3545"),
            BorderWidth = 1,
            FontSize = 13,
            CornerRadius = 6,
            HeightRequest = 40,
            WidthRequest = 100
        };
        deleteButton.Clicked += async (s, e) => await OnDeleteMacIdClicked(log);
        actionsStack.Add(deleteButton);

        // Reset Device Binding Button
        var resetDeviceButton = new Button
        {
            Text = "🔄 Unbind",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#0066ff"), // Blue text
            BorderColor = Color.FromArgb("#0066ff"),
            BorderWidth = 1,
            FontSize = 13,
            CornerRadius = 6,
            HeightRequest = 40,
            WidthRequest = 100
        };
        resetDeviceButton.Clicked += async (s, e) => await OnResetDeviceBindingClicked(log);
        actionsStack.Add(resetDeviceButton);
        
        grid.Add(actionsStack, 1, 0);
        Grid.SetRowSpan(actionsStack, 5);

        frame.Content = grid;
        return frame;
    }

    private async Task OnDeleteMacIdClicked(MacIdLogModel log)
    {
        var confirm = await DisplayAlert("Confirm Delete", 
            $"Are you sure you want to permanently delete MAC ID {log.MacId}?\nThis will remove the tracking log.", 
            "Delete", "Cancel");
        
        if (confirm)
        {
            try
            {
                var success = await _mongoService.DeleteMacIdLogAsync(log.MacId);
                if (success)
                {
                    await NotificationService.ShowSuccess("MAC ID deleted successfully");
                    await LoadMacIdsAsync();
                }
                else
                {
                    await NotificationService.ShowError("Failed to delete MAC ID");
                }
            }
            catch (Exception ex)
            {
                await NotificationService.ShowError($"Deletion failed: {ex.Message}");
            }
        }
    }


    private async Task OnResetDeviceBindingClicked(MacIdLogModel log)
    {
        var confirm = await DisplayAlert("Confirm Device Unbind", 
            $"Are you sure you want to unbind MAC ID {log.MacId} from user '{log.UserName}'?\n\nThis will allow the user to login from a NEW device immediately.", 
            "Unbind", "Cancel");
        
        if (confirm)
        {
            try
            {
                var success = await _mongoService.ResetUserDeviceBindingAsync(log.UserId);
                if (success)
                {
                    await NotificationService.ShowSuccess($"Device unbound successfully for {log.UserName}");
                }
                else
                {
                    await NotificationService.ShowError("Failed to unbind device. User may not exist or already unbound.");
                }
            }
            catch (Exception ex)
            {
                await NotificationService.ShowError($"Unbind failed: {ex.Message}");
            }
        }
    }

    private async Task OnBanUnbanMacIdClicked(MacIdLogModel log)
    {
        var action = log.IsBanned ? "unban" : "ban";
        var confirm = await DisplayAlert("Confirm", 
            $"Are you sure you want to {action} MAC ID {log.MacId}?\nThis will affect user: {log.UserName}", 
            "Yes", "No");
        
        if (confirm)
        {
            try
            {
                bool success;
                if (log.IsBanned)
                {
                    success = await _mongoService.UnbanMacIdAsync(log.MacId);
                }
                else
                {
                    success = await _mongoService.BanMacIdAsync(log.MacId);
                }

                if (success)
                {
                    await NotificationService.ShowSuccess($"MAC ID {action}ned successfully!");
                    await LoadMacIdsAsync();
                }
                else
                {
                    await NotificationService.ShowError("Failed to update MAC ID status");
                }
            }
            catch (Exception ex)
            {
                await NotificationService.ShowError(ex.Message);
            }
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadMacIdsAsync();
    }
}

using CarrotDownload.Maui.Services;
namespace CarrotDownload.Maui.Views;

public partial class AdminDashboardPage : ContentPage
{
    private readonly CarrotDownload.Database.CarrotMongoService _mongoService;

    public AdminDashboardPage(CarrotDownload.Database.CarrotMongoService mongoService)
    {
        InitializeComponent();
        _mongoService = mongoService;
        
        // Wire up navigation events
        AdminNavBar.DashboardClicked += (s, e) => { /* Already on Dashboard */ };
        AdminNavBar.UsersClicked += OnUsersNavClicked;
        AdminNavBar.MacIdsClicked += OnMacIdsNavClicked;
        AdminNavBar.LogoutClicked += OnLogoutNavClicked;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Set active page every time the page appears
        AdminNavBar.SetActivePage("Dashboard");
        
        await LoadStatisticsAsync();
    }

    private async Task LoadStatisticsAsync()
    {
        try
        {
            Console.WriteLine("[AdminDashboard] Loading statistics...");
            
            var usersCount = await _mongoService.GetTotalUsersCountAsync();
            var projectsCount = await _mongoService.GetTotalProjectsCountAsync();
            var filesCount = await _mongoService.GetTotalProgrammingFilesCountAsync();
            var playlistsCount = await _mongoService.GetTotalPlaylistsCountAsync();

            UsersCountLabel.Text = usersCount.ToString();
            ProjectsCountLabel.Text = projectsCount.ToString();
            FilesCountLabel.Text = filesCount.ToString();
            PlaylistsCountLabel.Text = playlistsCount.ToString();
            
            Console.WriteLine($"[AdminDashboard] Statistics loaded - Users: {usersCount}, Projects: {projectsCount}, Files: {filesCount}, Playlists: {playlistsCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminDashboard] Failed to load statistics: {ex.Message}");
            Console.WriteLine($"[AdminDashboard] Stack trace: {ex.StackTrace}");
            
            // Set default values instead of crashing
            UsersCountLabel.Text = "0";
            ProjectsCountLabel.Text = "0";
            FilesCountLabel.Text = "0";
            PlaylistsCountLabel.Text = "0";
            
            await NotificationService.ShowError($"Failed to load statistics: {ex.Message}");
        }
    }

    private async void OnUsersNavClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AdminUserManagementPage(_mongoService));
    }

    private async void OnMacIdsNavClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AdminMacIdManagementPage(_mongoService));
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadStatisticsAsync();
        await NotificationService.ShowSuccess("Statistics refreshed!");
    }

    private async void OnLogoutNavClicked(object sender, EventArgs e)
    {
        var dialog = new ConfirmationDialog("Logout", "Are you sure you want to logout?", "Yes", "No");
        await Navigation.PushModalAsync(dialog);
        if (await dialog.GetResultAsync())
        {
            await SecureStorage.Default.SetAsync("is_admin", "false");
            Application.Current.MainPage = new AppShell();
        }
    }
}

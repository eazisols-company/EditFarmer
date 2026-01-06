namespace CarrotDownload.Maui.Controls;

public partial class AdminNavigationBar : ContentView
{
    public event EventHandler? DashboardClicked;
    public event EventHandler? UsersClicked;
    public event EventHandler? MacIdsClicked;
    public event EventHandler? LogoutClicked;

    private string _currentPage = "Dashboard";

    public AdminNavigationBar()
    {
        InitializeComponent();
        UpdateActiveState();
    }

    public void SetActivePage(string pageName)
    {
        _currentPage = pageName;
        UpdateActiveState();
    }

    private void UpdateActiveState()
    {
        // Safety check to prevent crash if called before UI is ready
        if (DashboardLabel == null) return;

        // Reset all to default
        DashboardLabel.TextColor = Color.FromArgb("#333");
        DashboardLabel.FontAttributes = FontAttributes.None;
        UsersLabel.TextColor = Color.FromArgb("#333");
        UsersLabel.FontAttributes = FontAttributes.None;
        MacIdsLabel.TextColor = Color.FromArgb("#333");
        MacIdsLabel.FontAttributes = FontAttributes.None;
        LogoutLabel.TextColor = Color.FromArgb("#333");
        LogoutLabel.FontAttributes = FontAttributes.None;

        // Set active page
        switch (_currentPage)
        {
            case "Dashboard":
                DashboardLabel.TextColor = Color.FromArgb("#ff5722");
                DashboardLabel.FontAttributes = FontAttributes.Bold;
                break;
            case "Users":
                UsersLabel.TextColor = Color.FromArgb("#ff5722");
                UsersLabel.FontAttributes = FontAttributes.Bold;
                break;
            case "MacIds":
                MacIdsLabel.TextColor = Color.FromArgb("#ff5722");
                MacIdsLabel.FontAttributes = FontAttributes.Bold;
                break;
        }
    }

    private void OnDashboardClicked(object sender, EventArgs e)
    {
        SetActivePage("Dashboard");
        DashboardClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnUsersClicked(object sender, EventArgs e)
    {
        SetActivePage("Users");
        UsersClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnMacIdsClicked(object sender, EventArgs e)
    {
        SetActivePage("MacIds");
        MacIdsClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnLogoutClicked(object sender, EventArgs e)
    {
        LogoutClicked?.Invoke(this, EventArgs.Empty);
    }
}

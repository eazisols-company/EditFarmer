using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace CarrotDownload.Maui.Views;

public partial class ConfirmationDialog : ContentPage
{
    private TaskCompletionSource<bool> _tcs;

    public ConfirmationDialog(string title, string message, string confirmText = "Yes", string cancelText = "No", string? colorOverride = null)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        ConfirmButton.Text = confirmText;
        CancelButton.Text = cancelText;

        // Determine Color to use
        string themeColor = Preferences.Get("AccentColor", "#ff5722"); // Default to orange branding
        string colorToUse = colorOverride;

        if (string.IsNullOrEmpty(colorToUse))
        {
            // If it's a delete operation, use warning red by default
            if (confirmText.Equals("Delete", StringComparison.OrdinalIgnoreCase))
            {
                colorToUse = "#dc3545"; // Bootstrap Danger Red
            }
            else
            {
                colorToUse = themeColor;
            }
        }

        try
        {
            var parsedColor = Color.Parse(colorToUse);
            ConfirmButton.BackgroundColor = parsedColor;
            
            // Safety check: if background is too light/white, adjust text color or add border
            // A luminosity > 0.7 is generally where white text starts becoming hard to read
            if (parsedColor.GetLuminosity() > 0.7 || colorToUse.Equals("#ffffff", StringComparison.OrdinalIgnoreCase))
            {
                ConfirmButton.TextColor = Color.Parse("#333333");
                ConfirmButton.BorderColor = Color.Parse("#cccccc");
                ConfirmButton.BorderWidth = 1;
            }
            else
            {
                ConfirmButton.TextColor = Colors.White;
                ConfirmButton.BorderWidth = 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Color parse error: {ex.Message}");
            ConfirmButton.BackgroundColor = Color.FromRgb(255, 87, 34); // Brand orange
            ConfirmButton.TextColor = Colors.White;
            ConfirmButton.BorderWidth = 0;
        }

        _tcs = new TaskCompletionSource<bool>();
    }

    public Task<bool> GetResultAsync()
    {
        return _tcs.Task;
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(true);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(false);
        await Navigation.PopModalAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Parallel animation: Fade in overlay + Scale up & Fade in dialog
        await Task.WhenAll(
            OverlayGrid.FadeTo(1, 200),
            DialogBorder.ScaleTo(1, 250, Easing.SpringOut),
            DialogBorder.FadeTo(1, 200)
        );
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.TrySetResult(false);
        }
    }
}

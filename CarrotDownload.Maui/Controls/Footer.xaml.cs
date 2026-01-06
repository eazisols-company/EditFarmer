using CarrotDownload.Maui.Views;

namespace CarrotDownload.Maui.Controls;

public partial class Footer : ContentView
{
	public Footer()
	{
		InitializeComponent();
		LoadAccentColor();
		SubscribeToAccentColorChanges();
	}

	private void LoadAccentColor()
	{
		// Load accent color from preferences
		var accentColor = Preferences.Get("AccentColor", "#ff5722");
		ApplyAccentColor(accentColor);
	}

	private void SubscribeToAccentColorChanges()
	{
		// Subscribe to accent color change messages
		MessagingCenter.Subscribe<SettingsPage, string>(this, "AccentColorChanged", (sender, color) =>
		{
			ApplyAccentColor(color);
		});
	}

	public void ApplyAccentColor(string colorHex)
	{
		try
		{
			FooterBorder.BackgroundColor = Color.Parse(colorHex);
		}
		catch
		{
			// Fallback to default color if parsing fails
			FooterBorder.BackgroundColor = Color.Parse("#ff5722");
		}
	}
}

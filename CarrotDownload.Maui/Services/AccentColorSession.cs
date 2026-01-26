namespace CarrotDownload.Maui.Services;

// Simple in-memory accent color store for the current app session.
// Requirements:
// - Default to white on app start.
// - If user picks a color, keep it for the current app session (even across logouts).
// - On app close/relaunch, revert to white.
public static class AccentColorSession
{
	private static string _currentColor = "#ffffff"; // default white per session

	public static string CurrentColor
	{
		get => _currentColor;
	}

	public static void SetColor(string colorHex)
	{
		if (!string.IsNullOrWhiteSpace(colorHex))
		{
			_currentColor = colorHex;
		}
	}

	public static void Reset()
	{
		_currentColor = "#ffffff";
	}
}

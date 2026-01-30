using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CarrotDownload.Maui.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();
		// Keep mutex alive for Inno Setup detection
		_mutex = new System.Threading.Mutex(true, "EditFarmer_Mutex_A1B2C3D4");
		
		// Initialize hand cursor for all buttons
		CarrotDownload.Maui.Platforms.Windows.ButtonCursorHandler.Initialize();
	}

	private static System.Threading.Mutex _mutex;

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}


using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

namespace CarrotDownload.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if WINDOWS
		builder.ConfigureMauiHandlers(handlers =>
		{
			handlers.AddHandler(typeof(CarrotDownload.Maui.Controls.NativeMediaPlayerView), typeof(CarrotDownload.Maui.Controls.NativeMediaPlayerViewHandler));
		});
#endif

		// Register HTTP Client
		builder.Services.AddHttpClient<CarrotDownload.Auth.Interfaces.IAuthService, CarrotDownload.Auth.Services.AuthService>(client =>
		{
			// TODO: Replace with actual API base URL
			client.BaseAddress = new Uri("https://your-api-url.com/api");
			client.Timeout = TimeSpan.FromSeconds(30);
		});

		// Register Auth Services
		builder.Services.AddSingleton<CarrotDownload.Auth.Interfaces.IDeviceInfoService, CarrotDownload.Auth.Services.DeviceInfoService>();
		builder.Services.AddSingleton<CarrotDownload.Auth.Interfaces.ISecureStorageService, CarrotDownload.Maui.Services.SecureStorageService>();

		// Register FFmpeg Service
		builder.Services.AddSingleton<CarrotDownload.FFmpeg.Interfaces.IFFmpegService, CarrotDownload.FFmpeg.Services.FFmpegService>();
		builder.Services.AddSingleton<CarrotDownload.Core.Interfaces.IMediaJobQueue, CarrotDownload.Maui.Services.MediaJobQueueService>();

		// Register Database Services
		var connectionString = "mongodb+srv://KILODB:rocketbottle@cluster0-5z0fd.mongodb.net/test?retryWrites=true&w=majority";
		builder.Services.AddSingleton<CarrotDownload.Database.CarrotMongoService>(s => new CarrotDownload.Database.CarrotMongoService(connectionString));

		// Register Pages
		builder.Services.AddTransient<Views.LoginPage>();
		builder.Services.AddTransient<Views.DashboardPage>();
		builder.Services.AddTransient<Views.MediaProcessingPage>();
		builder.Services.AddTransient<Views.JobHistoryPage>();
		builder.Services.AddTransient<Views.DownloadsPage>();
		builder.Services.AddTransient<Views.StorePage>();
		builder.Services.AddTransient<Views.DigiPage>();
		builder.Services.AddTransient<Views.ManagerPage>();
		builder.Services.AddTransient<Views.AuctionPage>();
		builder.Services.AddTransient<Views.ProgrammingPage>();
		builder.Services.AddTransient<Views.SequencePage>();
		builder.Services.AddTransient<Views.SettingsPage>();
		builder.Services.AddTransient<Views.MediaPlayerPage>();
		
		// Register Admin Pages
		builder.Services.AddTransient<Views.AdminDashboardPage>();
		builder.Services.AddTransient<Views.AdminUserManagementPage>();
		builder.Services.AddTransient<Views.AdminMacIdManagementPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Remove Borders for BorderlessEntry on Windows
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
		{
			if (view is CarrotDownload.Maui.Controls.BorderlessEntry)
			{
#if WINDOWS
				handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
				handler.PlatformView.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
				handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.Margin = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.MinHeight = 0;
				handler.PlatformView.MinWidth = 0;
				handler.PlatformView.UseSystemFocusVisuals = false;

				// Overriding WinUI 3 Resources to kill borders and brushes in all states
				var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
				handler.PlatformView.Resources["TextControlBorderThickness"] = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.Resources["TextControlBorderThicknessPointerOver"] = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.Resources["TextControlBorderThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.Resources["TextControlBorderThicknessDisabled"] = new Microsoft.UI.Xaml.Thickness(0);
				
				handler.PlatformView.Resources["TextControlBorderBrush"] = transparentBrush;
				handler.PlatformView.Resources["TextControlBorderBrushPointerOver"] = transparentBrush;
				handler.PlatformView.Resources["TextControlBorderBrushFocused"] = transparentBrush;
				handler.PlatformView.Resources["TextControlBorderBrushDisabled"] = transparentBrush;
				
				// Hide the underline/bottom border specifically
				handler.PlatformView.Resources["TextControlUnderlineThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
#endif
			}
		});

		return builder.Build();
	}
}

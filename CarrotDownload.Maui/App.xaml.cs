namespace CarrotDownload.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

#if WINDOWS
		// Universal Fix for Windows: Prevent automatic focus jumping to Entry fields.
		// We allow all FrameworkElements (Layouts, Borders, etc.) to capture focus on interaction,
        // preventing the WinUI focus-manager from looking for the first available TextBox.
		
		Microsoft.Maui.Handlers.LayoutHandler.Mapper.AppendToMapping("FocusRetention", (handler, view) =>
		{
			if (handler.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
			{
				element.AllowFocusOnInteraction = true;
                element.IsTabStop = true;
			}
		});

		Microsoft.Maui.Handlers.BorderHandler.Mapper.AppendToMapping("FocusRetention", (handler, view) =>
		{
			if (handler.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
			{
				element.AllowFocusOnInteraction = true;
                element.IsTabStop = true;
			}
		});

		Microsoft.Maui.Handlers.ScrollViewHandler.Mapper.AppendToMapping("FocusRetention", (handler, view) =>
		{
			if (handler.PlatformView is Microsoft.UI.Xaml.Controls.ScrollViewer scrollViewer)
			{
				scrollViewer.AllowFocusOnInteraction = true;
				scrollViewer.IsTabStop = true;
                scrollViewer.TabNavigation = Microsoft.UI.Xaml.Input.KeyboardNavigationMode.Once;
			}
		});
#endif

		MainPage = new AppShell();
	}
}

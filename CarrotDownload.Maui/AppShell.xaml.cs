using System.Linq;

namespace CarrotDownload.Maui;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
        Routing.RegisterRoute(nameof(Views.MediaProcessingPage), typeof(Views.MediaProcessingPage));
        Routing.RegisterRoute(nameof(Views.JobHistoryPage), typeof(Views.JobHistoryPage));
	}
}

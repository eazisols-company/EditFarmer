using Microsoft.Maui.Controls;

namespace CarrotDownload.Maui.Views;

public partial class ProcessPlaylistsDialog : ContentPage
{
	private TaskCompletionSource<bool> _taskCompletionSource;
	
	public ProcessPlaylistsDialog(int playlistCount)
	{
		System.Diagnostics.Debug.WriteLine("ProcessPlaylistsDialog constructor called");
		InitializeComponent();
		
		// Update confirmation text with playlist count
		ConfirmLabel.Text = $"Confirm Processing {playlistCount} Playlist{(playlistCount != 1 ? "s" : "")}";
		
		_taskCompletionSource = new TaskCompletionSource<bool>();
		System.Diagnostics.Debug.WriteLine("ProcessPlaylistsDialog initialized");
	}
	
	public Task<bool> GetResultAsync()
	{
		System.Diagnostics.Debug.WriteLine("GetResultAsync called");
		return _taskCompletionSource.Task;
	}
	
	private async void OnYesProcessClicked(object sender, EventArgs e)
	{
		System.Diagnostics.Debug.WriteLine("Yes button clicked!");
		_taskCompletionSource.SetResult(true);
		await Navigation.PopModalAsync();
	}
	
	private async void OnCancelClicked(object sender, EventArgs e)
	{
		System.Diagnostics.Debug.WriteLine("Cancel button clicked!");
		_taskCompletionSource.SetResult(false);
		await Navigation.PopModalAsync();
	}
}

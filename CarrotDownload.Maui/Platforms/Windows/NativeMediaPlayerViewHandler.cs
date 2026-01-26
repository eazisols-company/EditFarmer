#if WINDOWS
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.IO;

namespace CarrotDownload.Maui.Controls;

public class NativeMediaPlayerViewHandler : ViewHandler<NativeMediaPlayerView, MediaPlayerElement>
{
	public static readonly IPropertyMapper<NativeMediaPlayerView, NativeMediaPlayerViewHandler> Mapper =
		new PropertyMapper<NativeMediaPlayerView, NativeMediaPlayerViewHandler>(ViewHandler.ViewMapper);

	public NativeMediaPlayerViewHandler() : base(Mapper)
	{
	}

	MediaPlayer? mediaPlayer;

	protected override MediaPlayerElement CreatePlatformView()
	{
		var playerElement = new MediaPlayerElement
		{
			AreTransportControlsEnabled = true,
			Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
		};

		// Configure transport controls to be more compact
		if (playerElement.TransportControls != null)
		{
			playerElement.TransportControls.IsCompact = true;
			playerElement.TransportControls.IsZoomButtonVisible = false;
			playerElement.TransportControls.IsPlaybackRateButtonVisible = false;
		}

		return playerElement;
	}

	protected override void ConnectHandler(MediaPlayerElement platformView)
	{
		base.ConnectHandler(platformView);

		VirtualView.FilePathChanged += OnFilePathChanged;
		VirtualView.AreTransportControlsEnabledChanged += OnControlsEnabledChanged;
		
		// Add tap handler for play/pause when controls are hidden
		platformView.Tapped += OnPlatformViewTapped;
		
		UpdateSource();
		UpdateControls();
	}

	protected override void DisconnectHandler(MediaPlayerElement platformView)
	{
		VirtualView.FilePathChanged -= OnFilePathChanged;
		VirtualView.AreTransportControlsEnabledChanged -= OnControlsEnabledChanged;
		platformView.Tapped -= OnPlatformViewTapped;

		try
		{
			platformView.SetMediaPlayer(null);
		}
		catch
		{
			// ignore
		}

		mediaPlayer?.Dispose();
		mediaPlayer = null;

		base.DisconnectHandler(platformView);
	}

	void OnFilePathChanged(object? sender, EventArgs e) => UpdateSource();
	void OnControlsEnabledChanged(object? sender, EventArgs e) => UpdateControls();

	void OnPlatformViewTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
	{
		if (!VirtualView.AreTransportControlsEnabled && mediaPlayer != null)
		{
			if (mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
			{
				mediaPlayer.Pause();
			}
			else
			{
				mediaPlayer.Play();
			}
		}
	}


	void UpdateControls()
	{
		if (PlatformView != null)
		{
			PlatformView.AreTransportControlsEnabled = VirtualView.AreTransportControlsEnabled;
		}
	}

	void UpdateSource()
	{
		if (PlatformView is null)
		{
			return;
		}

		var path = VirtualView.FilePath;
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return;
		}

		mediaPlayer?.Dispose();
		mediaPlayer = new MediaPlayer();
		mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(path));
		
		mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;

		PlatformView.SetMediaPlayer(mediaPlayer);

		if (VirtualView.ShouldAutoPlay)
		{
			mediaPlayer.Play();
		}
	}

	void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
	{
		if (VirtualView != null)
		{
			// Marshal to UI thread
			PlatformView.DispatcherQueue.TryEnqueue(() => 
			{
				VirtualView.IsPlaying = sender.PlaybackState == MediaPlaybackState.Playing;
			});
		}
	}
}
#endif


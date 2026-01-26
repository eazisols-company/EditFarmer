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
		try
		{
			if (PlatformView is null || VirtualView is null)
			{
				return;
			}

			var path = VirtualView.FilePath;
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				// Clear existing player if file doesn't exist
				if (mediaPlayer != null)
				{
					try
					{
						PlatformView.SetMediaPlayer(null);
						mediaPlayer.Dispose();
						mediaPlayer = null;
					}
					catch
					{
						// Ignore disposal errors
					}
				}
				return;
			}

			// Dispose existing player safely
			if (mediaPlayer != null)
			{
				try
				{
					mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
					PlatformView.SetMediaPlayer(null);
					mediaPlayer.Dispose();
				}
				catch
				{
					// Ignore disposal errors
				}
				finally
				{
					mediaPlayer = null;
				}
			}

			// Create new player with error handling
			mediaPlayer = new MediaPlayer();
			mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(path));
			mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;

			PlatformView.SetMediaPlayer(mediaPlayer);

			if (VirtualView.ShouldAutoPlay)
			{
				mediaPlayer.Play();
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[MediaPlayer] Error updating source: {ex.Message}");
			// Clean up on error
			if (mediaPlayer != null)
			{
				try
				{
					mediaPlayer.Dispose();
				}
				catch
				{
					// Ignore
				}
				finally
				{
					mediaPlayer = null;
				}
			}
		}
	}

	void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
	{
		try
		{
			if (VirtualView != null && PlatformView != null && PlatformView.DispatcherQueue != null)
			{
				// Marshal to UI thread
				PlatformView.DispatcherQueue.TryEnqueue(() => 
				{
					if (VirtualView != null)
					{
						VirtualView.IsPlaying = sender.PlaybackState == MediaPlaybackState.Playing;
					}
				});
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[MediaPlayer] Error in playback state changed: {ex.Message}");
		}
	}
}
#endif


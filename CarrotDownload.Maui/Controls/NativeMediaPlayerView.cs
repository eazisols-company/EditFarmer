namespace CarrotDownload.Maui.Controls;

public class NativeMediaPlayerView : View
{
	public static readonly BindableProperty FilePathProperty =
		BindableProperty.Create(
			nameof(FilePath),
			typeof(string),
			typeof(NativeMediaPlayerView),
			default(string),
			propertyChanged: static (bindable, _, _) =>
			{
				var view = (NativeMediaPlayerView)bindable;
				view.FilePathChanged?.Invoke(view, EventArgs.Empty);
			});

	public static readonly BindableProperty ShouldAutoPlayProperty =
		BindableProperty.Create(
			nameof(ShouldAutoPlay),
			typeof(bool),
			typeof(NativeMediaPlayerView),
			false,
			propertyChanged: static (bindable, _, _) =>
			{
				var view = (NativeMediaPlayerView)bindable;
				view.FilePathChanged?.Invoke(view, EventArgs.Empty);
			});

	public string FilePath
	{
		get => (string)GetValue(FilePathProperty);
		set => SetValue(FilePathProperty, value);
	}

	public bool ShouldAutoPlay
	{
		get => (bool)GetValue(ShouldAutoPlayProperty);
		set => SetValue(ShouldAutoPlayProperty, value);
	}

	public static readonly BindableProperty AreTransportControlsEnabledProperty =
		BindableProperty.Create(
			nameof(AreTransportControlsEnabled),
			typeof(bool),
			typeof(NativeMediaPlayerView),
			true, // Default to true
			propertyChanged: static (bindable, _, _) =>
			{
				var view = (NativeMediaPlayerView)bindable;
				view.AreTransportControlsEnabledChanged?.Invoke(view, EventArgs.Empty);
			});

	public bool AreTransportControlsEnabled
	{
		get => (bool)GetValue(AreTransportControlsEnabledProperty);
		set => SetValue(AreTransportControlsEnabledProperty, value);
	}

	public static readonly BindableProperty IsPlayingProperty =
		BindableProperty.Create(
			nameof(IsPlaying),
			typeof(bool),
			typeof(NativeMediaPlayerView),
			false,
			defaultBindingMode: BindingMode.TwoWay);

	public bool IsPlaying
	{
		get => (bool)GetValue(IsPlayingProperty);
		set => SetValue(IsPlayingProperty, value);
	}

	internal event EventHandler? FilePathChanged;
	internal event EventHandler? AreTransportControlsEnabledChanged;
}


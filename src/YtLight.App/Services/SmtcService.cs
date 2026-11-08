using Windows.Media;
using Windows.Media.Playback;
using YtLight.Core.Abstractions;
using YtLight.Core.Models;

namespace YtLight.App.Services;

public sealed class SmtcService : ISmtcService
{
    private readonly MediaPlayer _holder;
    private readonly SystemMediaTransportControls _smtc;
    private IAudioEngine? _engine;

    public SmtcService()
    {
        _holder = new MediaPlayer { CommandManager = { IsEnabled = false } };
        _smtc = _holder.SystemMediaTransportControls;
        _smtc.IsEnabled = true;
        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
        _smtc.IsNextEnabled = true;
        _smtc.IsPreviousEnabled = true;
        _smtc.ButtonPressed += OnButtonPressed;
    }

    public void Attach(IAudioEngine engine)
    {
        _engine = engine;
        engine.StateChanged += (_, _) => Update(engine.CurrentTrack, engine.IsPlaying, engine.Position, engine.Duration);
        engine.TrackChanged += (_, _) => Update(engine.CurrentTrack, engine.IsPlaying, engine.Position, engine.Duration);
    }

    public void Update(Track? track, bool isPlaying, TimeSpan position, TimeSpan duration)
    {
        _smtc.PlaybackStatus = isPlaying
            ? MediaPlaybackStatus.Playing
            : track is null ? MediaPlaybackStatus.Stopped : MediaPlaybackStatus.Paused;

        var updater = _smtc.DisplayUpdater;
        updater.Type = MediaPlaybackType.Music;
        updater.MusicProperties.Title = track?.Title ?? "YtLight";
        updater.MusicProperties.Artist = track?.Artists ?? "";
        updater.AppMediaId = "YtLight";
        updater.Update();
    }

    private async void OnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        if (_engine is null)
            return;

        switch (args.Button)
        {
            case SystemMediaTransportControlsButton.Play:
            case SystemMediaTransportControlsButton.Pause:
                await _engine.PlayPauseAsync().ConfigureAwait(false);
                break;
            case SystemMediaTransportControlsButton.Next:
                await _engine.NextAsync().ConfigureAwait(false);
                break;
            case SystemMediaTransportControlsButton.Previous:
                await _engine.PreviousAsync().ConfigureAwait(false);
                break;
        }
    }

    public void Dispose()
    {
        _smtc.ButtonPressed -= OnButtonPressed;
        _holder.Dispose();
    }
}

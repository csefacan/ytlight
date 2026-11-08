using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtLight.Core.Abstractions;
using YtLight.Core.Models;
using YtLight.Core.Session;
using YtLight.InnerTube;
using YtLight.Playback;

namespace YtLight.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IInnerTubeClient _client;
    private readonly IProfileService _profiles;
    private readonly ISessionStore _store;
    private readonly PlaybackHistoryReporter _history;
    private bool _isUpdatingProfile;

    public MainViewModel(
        IInnerTubeClient client,
        IProfileService profiles,
        ISessionStore store,
        LibraryViewModel library,
        ExploreViewModel explore,
        PlayerBarViewModel player,
        PlaybackHistoryReporter history)
    {
        _client = client;
        _profiles = profiles;
        _store = store;
        _history = history;
        Library = library;
        Explore = explore;
        Player = player;

        _profiles.ActiveProfileChanged += async (_, _) =>
        {
            if (_isUpdatingProfile) return;
            _isUpdatingProfile = true;
            try
            {
                var matched = Profiles.FirstOrDefault(p => p.IsolationKey == _profiles.ActiveProfile?.IsolationKey)
                              ?? _profiles.ActiveProfile;
                ActiveProfile = matched;
            }
            finally
            {
                _isUpdatingProfile = false;
            }
            await ReloadForProfileAsync().ConfigureAwait(true);
        };
    }

    public LibraryViewModel Library { get; }
    public ExploreViewModel Explore { get; }
    public PlayerBarViewModel Player { get; }
    public ObservableCollection<YoutubeProfile> Profiles { get; } = [];
    public ObservableCollection<Track> SearchSongs { get; } = [];

    [ObservableProperty] private bool isSignedIn;
    [ObservableProperty] private YoutubeProfile? activeProfile;
    [ObservableProperty] private string searchQuery = "";
    [ObservableProperty] private bool isSearchOpen;
    [ObservableProperty] private string banner = "YtLight";
    [ObservableProperty] private int selectedTab;

    partial void OnActiveProfileChanged(YoutubeProfile? oldValue, YoutubeProfile? newValue)
    {
        if (_isUpdatingProfile || newValue is null)
            return;

        if (oldValue is not null && oldValue.IsolationKey == newValue.IsolationKey && _client.ActiveProfile?.IsolationKey == newValue.IsolationKey)
            return;

        _ = SwitchProfileAsync(newValue);
    }

    private async Task SwitchProfileAsync(YoutubeProfile profile)
    {
        _isUpdatingProfile = true;
        try
        {
            await _profiles.SelectAsync(profile).ConfigureAwait(true);
        }
        finally
        {
            _isUpdatingProfile = false;
        }

        await ReloadForProfileAsync().ConfigureAwait(true);
    }

    public async Task InitializeAsync()
    {
        var session = await _store.LoadAsync().ConfigureAwait(true);
        if (session is null)
        {
            IsSignedIn = false;
            Banner = "Giriş yaparak kütüphanenizi açın";
            return;
        }

        await ApplySessionAsync(session).ConfigureAwait(true);
    }

    public async Task CompleteLoginAsync(AuthSession session)
    {
        await _store.SaveAsync(session).ConfigureAwait(true);
        await ApplySessionAsync(session).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _store.ClearAsync().ConfigureAwait(true);
        _client.SetSession(null);
        _history.BindSession(null);
        IsSignedIn = false;
        Profiles.Clear();
        ActiveProfile = null;
        Banner = "Oturum kapatıldı";
        await Library.ReloadAsync().ConfigureAwait(true);
        await Explore.ReloadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SelectProfileAsync(YoutubeProfile? profile)
    {
        if (profile is null) return;
        await SwitchProfileAsync(profile);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            IsSearchOpen = false;
            return;
        }

        try
        {
            var result = await _client.SearchAsync(SearchQuery.Trim()).ConfigureAwait(true);
            SearchSongs.Clear();
            foreach (var s in result.Songs.Take(40))
                SearchSongs.Add(s);
            IsSearchOpen = true;
            Banner = $"Arama: {result.Songs.Count} şarkı bulundu";
        }
        catch (Exception ex)
        {
            Banner = "Arama başarısız: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task PlayTrackAsync(Track? track)
    {
        if (track is null) return;
        Banner = $"Yükleniyor: {track.Title}";
        var queue = Library.Tracks.Count > 0 ? Library.Tracks.ToList() : new List<Track> { track };
        await Player.PlayAsync(track, queue).ConfigureAwait(true);
        Banner = $"Çalınıyor: {track.Title}";
    }

    [RelayCommand]
    private async Task AddToSelectedPlaylistAsync(Track? track)
    {
        if (track is null || Library.SelectedPlaylist is null) return;
        try
        {
            await _client.AddToPlaylistAsync(Library.SelectedPlaylist.PlaylistId, track.VideoId).ConfigureAwait(true);
            Banner = $"Eklendi: {Library.SelectedPlaylist.Title}";
        }
        catch (Exception ex)
        {
            Banner = "Eklenemedi: " + ex.Message;
        }
    }

    private async Task ApplySessionAsync(AuthSession session)
    {
        _client.SetSession(session);
        _history.BindSession(session);
        try
        {
            await _client.BootstrapAsync().ConfigureAwait(true);
            var list = await _profiles.RefreshAsync().ConfigureAwait(true);
            Profiles.Clear();
            foreach (var p in list)
                Profiles.Add(p);

            if (Profiles.Count == 0)
            {
                var def = new YoutubeProfile { DisplayName = "Varsayılan Kanal", IsSelected = true };
                Profiles.Add(def);
                await _profiles.SelectAsync(def).ConfigureAwait(true);
            }

            var savedKey = session.ActiveProfile?.IsolationKey ?? _profiles.ActiveProfile?.IsolationKey;
            var matched = Profiles.FirstOrDefault(p => p.IsolationKey == savedKey)
                          ?? Profiles.FirstOrDefault(p => p.IsSelected)
                          ?? Profiles.FirstOrDefault();

            _isUpdatingProfile = true;
            try
            {
                ActiveProfile = matched;
                if (matched is not null)
                    await _profiles.SelectAsync(matched).ConfigureAwait(true);
            }
            finally
            {
                _isUpdatingProfile = false;
            }

            IsSignedIn = true;
            Banner = ActiveProfile is null ? "Giriş yapıldı" : ActiveProfile.DisplayName;
            await ReloadForProfileAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            IsSignedIn = session.IsAuthenticated;
            Banner = "Oturum açık (Kanal bilgisi alınamadı): " + ex.Message;
            await ReloadForProfileAsync().ConfigureAwait(true);
        }
    }

    private async Task ReloadForProfileAsync()
    {
        Banner = ActiveProfile?.DisplayName ?? "YtLight";
        var session = await _store.LoadAsync().ConfigureAwait(true);
        if (session != null)
            _history.BindSession(session.WithProfile(ActiveProfile));

        await Library.ReloadAsync().ConfigureAwait(true);
        if (SelectedTab == 1)
            await Explore.ReloadAsync().ConfigureAwait(true);
    }

    public Task LoadExploreIfNeededAsync() => Explore.ReloadAsync();
}

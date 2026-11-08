using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtLight.Core.Abstractions;
using YtLight.Core.Models;

namespace YtLight.App.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IInnerTubeClient _client;

    public LibraryViewModel(IInnerTubeClient client) => _client = client;

    public ObservableCollection<PlaylistSummary> Playlists { get; } = [];
    public ObservableCollection<AlbumSummary> Albums { get; } = [];
    public ObservableCollection<Track> Tracks { get; } = [];

    [ObservableProperty] private PlaylistSummary? selectedPlaylist;
    [ObservableProperty] private AlbumSummary? selectedAlbum;
    [ObservableProperty] private string status = "Giriş yapın";
    [ObservableProperty] private bool isBusy;

    partial void OnSelectedPlaylistChanged(PlaylistSummary? value)
    {
        if (value is not null)
        {
            SelectedAlbum = null;
            _ = LoadTracksAsync(value);
        }
    }

    partial void OnSelectedAlbumChanged(AlbumSummary? value)
    {
        if (value is not null)
        {
            _ = LoadAlbumAsync(value);
        }
    }

    public async Task ReloadAsync()
    {
        if (!_client.IsAuthenticated)
        {
            Playlists.Clear();
            Albums.Clear();
            Tracks.Clear();
            Status = "Giriş yapın";
            return;
        }

        IsBusy = true;
        Status = "Kütüphane yükleniyor…";
        try
        {
            var snap = await _client.GetLibraryAsync().ConfigureAwait(true);
            Playlists.Clear();
            foreach (var p in snap.Playlists)
                Playlists.Add(p);
            Albums.Clear();
            foreach (var a in snap.Albums)
                Albums.Add(a);
            Status = $"{Playlists.Count} liste · {Albums.Count} albüm";
            if (Playlists.Count > 0)
                SelectedPlaylist = Playlists[0];
        }
        catch (Exception ex)
        {
            Status = "Kütüphane alınamadı: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadTracksAsync(PlaylistSummary playlist)
    {
        IsBusy = true;
        try
        {
            var tracks = await _client.GetPlaylistTracksAsync(playlist.BrowseId).ConfigureAwait(true);
            Tracks.Clear();
            foreach (var t in tracks)
                Tracks.Add(t);
            playlist.TrackCount = Tracks.Count;
            Status = $"{playlist.Title} · {Tracks.Count} parça";
        }
        catch (Exception ex)
        {
            Status = "Liste açılamadı: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadAlbumAsync(AlbumSummary album)
    {
        IsBusy = true;
        try
        {
            var tracks = await _client.GetPlaylistTracksAsync(album.BrowseId).ConfigureAwait(true);
            Tracks.Clear();
            foreach (var t in tracks)
                Tracks.Add(t);
            Status = $"Albüm: {album.Title} · {Tracks.Count} parça";
        }
        catch (Exception ex)
        {
            Status = "Albüm açılamadı: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RemoveTrackFromCurrentPlaylistAsync(Track? track)
    {
        if (track is null || SelectedPlaylist is null || SelectedPlaylist.IsLikedSongs)
            return;

        try
        {
            await _client.RemoveFromPlaylistAsync(SelectedPlaylist.PlaylistId, track.VideoId, track.SetVideoId).ConfigureAwait(true);
            Tracks.Remove(track);
            Status = $"Çıkarıldı: {track.Title}";
        }
        catch (Exception ex)
        {
            Status = "Şarkı çıkarılamadı: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task CreatePlaylistAsync(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        try
        {
            var id = await _client.CreatePlaylistAsync(title.Trim()).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(id))
            {
                var summary = new PlaylistSummary
                {
                    PlaylistId = id,
                    Title = title.Trim(),
                    IsOwn = true
                };
                Playlists.Insert(1, summary);
                SelectedPlaylist = summary;
                Status = $"Çalma listesi oluşturuldu: {title}";
            }
        }
        catch (Exception ex)
        {
            Status = "Çalma listesi oluşturulamadı: " + ex.Message;
        }
    }
}

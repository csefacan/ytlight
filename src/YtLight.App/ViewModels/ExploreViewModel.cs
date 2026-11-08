using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using YtLight.Core.Abstractions;
using YtLight.Core.Models;

namespace YtLight.App.ViewModels;

public partial class ExploreViewModel : ObservableObject
{
    private readonly IInnerTubeClient _client;

    public ExploreViewModel(IInnerTubeClient client) => _client = client;

    public ObservableCollection<HomeShelf> Shelves { get; } = [];
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isBusy;

    public async Task ReloadAsync()
    {
        if (!_client.IsAuthenticated)
        {
            Shelves.Clear();
            Status = "Giriş yapın";
            return;
        }

        IsBusy = true;
        Status = "Öneriler yükleniyor…";
        try
        {
            var home = await _client.GetHomeAsync().ConfigureAwait(true);
            var explore = await _client.GetExploreAsync().ConfigureAwait(true);
            Shelves.Clear();
            foreach (var s in home.Shelves.Concat(explore.Shelves))
                Shelves.Add(s);
            Status = $"{Shelves.Count} raf";
        }
        catch (Exception ex)
        {
            Status = "Keşfet alınamadı: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

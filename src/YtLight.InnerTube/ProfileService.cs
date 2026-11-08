using YtLight.Core.Abstractions;
using YtLight.Core.Models;

namespace YtLight.InnerTube;

public sealed class ProfileService : IProfileService
{
    private readonly IInnerTubeClient _client;
    private readonly ISessionStore _store;
    private readonly List<YoutubeProfile> _profiles = [];

    public ProfileService(IInnerTubeClient client, ISessionStore store)
    {
        _client = client;
        _store = store;
    }

    public YoutubeProfile? ActiveProfile { get; private set; }
    public IReadOnlyList<YoutubeProfile> Profiles => _profiles;
    public event EventHandler? ActiveProfileChanged;

    public async Task<IReadOnlyList<YoutubeProfile>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _client.ListAccountsAsync(cancellationToken).ConfigureAwait(false);
        _profiles.Clear();
        _profiles.AddRange(accounts);

        var previousKey = ActiveProfile?.IsolationKey ?? _client.ActiveProfile?.IsolationKey;
        var selected = _profiles.FirstOrDefault(p => p.IsolationKey == previousKey)
                       ?? _profiles.FirstOrDefault(p => p.IsSelected)
                       ?? _profiles.FirstOrDefault();

        if (selected is not null)
            await SelectAsync(selected, persist: true, cancellationToken).ConfigureAwait(false);

        return _profiles;
    }

    public Task SelectAsync(YoutubeProfile profile, CancellationToken cancellationToken = default)
        => SelectAsync(profile, persist: true, cancellationToken);

    private async Task SelectAsync(YoutubeProfile profile, bool persist, CancellationToken cancellationToken)
    {
        var marked = _profiles.Select(p => p.WithSelected(p.IsolationKey == profile.IsolationKey)).ToList();
        _profiles.Clear();
        _profiles.AddRange(marked);
        ActiveProfile = marked.First(p => p.IsSelected);
        _client.SetActiveProfile(ActiveProfile);

        if (persist)
        {
            var session = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (session is not null)
                await _store.SaveAsync(session.WithProfile(ActiveProfile), cancellationToken).ConfigureAwait(false);
        }

        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }
}

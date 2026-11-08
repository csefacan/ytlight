namespace YtLight.Core.Models;

public sealed class YoutubeProfile
{
    public required string DisplayName { get; init; }
    public string? Handle { get; init; }
    public string? PhotoUrl { get; init; }
    public string? PageId { get; init; }
    public string? DatasyncId { get; init; }
    public string? OnBehalfOfUser { get; init; }
    public string? ChannelId { get; init; }
    public bool IsSelected { get; init; }
    public bool IsBrandAccount { get; init; }

    public string IsolationKey =>
        !string.IsNullOrWhiteSpace(PageId) ? PageId
        : !string.IsNullOrWhiteSpace(ChannelId) ? ChannelId
        : !string.IsNullOrWhiteSpace(OnBehalfOfUser) ? OnBehalfOfUser
        : !string.IsNullOrWhiteSpace(DatasyncId) ? DatasyncId
        : !string.IsNullOrWhiteSpace(Handle) ? Handle
        : DisplayName;

    public bool HasPageIsolation =>
        !string.IsNullOrWhiteSpace(PageId) || !string.IsNullOrWhiteSpace(OnBehalfOfUser);

    public YoutubeProfile WithSelected(bool selected) => new()
    {
        DisplayName = DisplayName,
        Handle = Handle,
        PhotoUrl = PhotoUrl,
        PageId = PageId,
        DatasyncId = DatasyncId,
        OnBehalfOfUser = OnBehalfOfUser,
        ChannelId = ChannelId,
        IsSelected = selected,
        IsBrandAccount = IsBrandAccount
    };
}

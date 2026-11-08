using System.Text.Json;
using YtLight.Core.Models;
using YtLight.InnerTube.Json;

namespace YtLight.InnerTube.Parsing;

public static class AccountListParser
{
    public static IReadOnlyList<YoutubeProfile> Parse(JsonElement root)
    {
        var profiles = new List<YoutubeProfile>();
        JsonWalk.Visit(root, el =>
        {
            if (el.ValueKind != JsonValueKind.Object)
                return;

            JsonElement renderer = default;
            if (el.TryGetProperty("accountItemRenderer", out var accountR)
                && accountR.ValueKind == JsonValueKind.Object)
            {
                renderer = accountR;
            }
            else if (el.TryGetProperty("accountItem", out var accountItem)
                && accountItem.ValueKind == JsonValueKind.Object)
            {
                renderer = accountItem;
            }
            else if (el.TryGetProperty("accountName", out _) || el.TryGetProperty("channelHandle", out _))
            {
                renderer = el;
            }

            if (renderer.ValueKind == JsonValueKind.Undefined)
                return;

            var profile = ParseRenderer(renderer);
            if (profile is not null
                && profiles.All(p => p.IsolationKey != profile.IsolationKey || p.DisplayName != profile.DisplayName))
            {
                profiles.Add(profile);
            }
        });

        if (profiles.Count == 0)
        {
            profiles.Add(new YoutubeProfile
            {
                DisplayName = "Varsayılan Kanal",
                IsSelected = true
            });
        }

        return profiles
            .OrderByDescending(p => p.IsSelected)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static YoutubeProfile? ParseRenderer(JsonElement renderer)
    {
        var name = FirstString(renderer,
            "accountName",
            "title",
            "displayName",
            "channelName",
            "name");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // Channel handle (@handle) preferred first, then byline
        var handle = FirstString(renderer,
            "channelHandle",
            "handle",
            "accountByline",
            "secondaryText");

        var datasyncId = FirstString(renderer,
            "datasyncIdToken",
            "datasyncId",
            "accountId",
            "userId");

        // ONLY treat pageIdToken or pageId as PageId. Never treat GaiaId as PageId,
        // because sending a personal GaiaId as X-Goog-PageId causes HTTP 401 Unauthorized!
        var pageId = FirstString(renderer, "pageId");

        var channelId = FirstString(renderer, "channelId", "externalId");

        // If pageId is present, this is a Brand Account requiring X-Goog-PageId
        var isBrand = !string.IsNullOrWhiteSpace(pageId);
        var onBehalf = isBrand ? pageId : null;

        var photo = renderer.TryGetProperty("accountPhoto", out var photoEl)
            ? JsonWalk.SmallestThumbnail(photoEl)
            : JsonWalk.SmallestThumbnail(renderer);

        var selected = renderer.TryGetProperty("isSelected", out var sel) && sel.ValueKind == JsonValueKind.True;

        return new YoutubeProfile
        {
            DisplayName = name,
            Handle = handle,
            PhotoUrl = photo,
            PageId = pageId,
            DatasyncId = datasyncId,
            OnBehalfOfUser = onBehalf,
            ChannelId = channelId,
            IsSelected = selected,
            IsBrandAccount = isBrand
        };
    }

    private static string? FirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            var parsed = JsonWalk.Text(value);
            if (!string.IsNullOrWhiteSpace(parsed))
                return parsed;
        }

        return JsonWalk.FindString(element, names);
    }
}

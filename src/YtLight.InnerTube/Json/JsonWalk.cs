using System.Text.Json;

namespace YtLight.InnerTube.Json;

internal static class JsonWalk
{
    public static string Text(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? "";

        if (element.TryGetProperty("simpleText", out var simple))
            return simple.GetString() ?? "";

        if (element.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
        {
            return string.Concat(runs.EnumerateArray()
                .Select(r => r.TryGetProperty("text", out var t) ? t.GetString() ?? "" : ""));
        }

        return "";
    }

    public static string? FindString(JsonElement element, params string[] names)
    {
        string? found = null;
        Visit(element, el =>
        {
            if (found is not null || el.ValueKind != JsonValueKind.Object)
                return;

            foreach (var name in names)
            {
                if (el.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        found = text;
                        return;
                    }
                }
            }
        });
        return found;
    }

    public static IEnumerable<JsonElement> FindPropertiesNamed(JsonElement root, string name)
    {
        var list = new List<JsonElement>();
        Visit(root, el =>
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var value))
                list.Add(value);
        });
        return list;
    }

    public static void Visit(JsonElement element, Action<JsonElement> visitor)
    {
        visitor(element);
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    Visit(prop.Value, visitor);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    Visit(item, visitor);
                break;
        }
    }

    public static TimeSpan ParseDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return TimeSpan.Zero;

        var parts = text.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var s))
            return new TimeSpan(0, m, s);
        if (parts.Length == 3
            && int.TryParse(parts[0], out var h)
            && int.TryParse(parts[1], out var mm)
            && int.TryParse(parts[2], out var ss))
            return new TimeSpan(h, mm, ss);

        return TimeSpan.Zero;
    }

    public static string? SmallestThumbnail(JsonElement parent)
    {
        if (!TryGetNested(parent, out var thumbs, "thumbnail", "thumbnails")
            && !TryGetNested(parent, out thumbs, "thumbnails")
            && !parent.TryGetProperty("thumbnails", out thumbs))
        {
            return null;
        }

        if (thumbs.ValueKind != JsonValueKind.Array)
            return null;

        string? best = null;
        var bestArea = int.MaxValue;
        foreach (var t in thumbs.EnumerateArray())
        {
            var url = t.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(url))
                continue;
            var w = t.TryGetProperty("width", out var ww) && ww.TryGetInt32(out var width) ? width : 120;
            var h = t.TryGetProperty("height", out var hh) && hh.TryGetInt32(out var height) ? height : 120;
            var area = w * h;
            if (area < bestArea)
            {
                bestArea = area;
                best = url;
            }
        }

        return best;
    }

    public static bool TryGetNested(JsonElement element, out JsonElement result, params string[] path)
    {
        result = element;
        foreach (var segment in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(segment, out result))
                return false;
        }

        return true;
    }
}

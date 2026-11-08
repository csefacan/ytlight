using System.Collections.Concurrent;

namespace YtLight.Core;

public sealed class ThumbnailCache
{
    private readonly ConcurrentDictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);
    private const int MaxEntries = 48;

    public bool TryGet(string url, out byte[] data) => _bytes.TryGetValue(url, out data!);

    public void Set(string url, byte[] data)
    {
        if (_bytes.Count >= MaxEntries)
        {
            foreach (var key in _bytes.Keys.Take(16).ToArray())
                _bytes.TryRemove(key, out _);
        }

        _bytes[url] = data;
    }

    public void Clear() => _bytes.Clear();
}

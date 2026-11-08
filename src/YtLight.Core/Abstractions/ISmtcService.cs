using YtLight.Core.Models;

namespace YtLight.Core.Abstractions;

public interface ISmtcService : IDisposable
{
    void Attach(IAudioEngine engine);
    void Update(Track? track, bool isPlaying, TimeSpan position, TimeSpan duration);
}

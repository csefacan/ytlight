namespace YtLight.Core.Models;

public sealed class QueueItem
{
    public required Track Track { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
}

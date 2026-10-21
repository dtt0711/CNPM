using System.Collections.Concurrent;
using TourismHcm.Application.Abstractions.Providers;

namespace TourismHcm.Infrastructure.Providers.Audio;

public sealed class AudioTaskRuntime : IAudioTaskRuntime
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public CancellationToken Register(Guid taskId, CancellationToken outerCt)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        _running.AddOrUpdate(
            taskId,
            linked,
            (_, existing) =>
            {
                existing.Dispose();
                return linked;
            });
        return linked.Token;
    }

    public void Unregister(Guid taskId)
    {
        if (_running.TryRemove(taskId, out var cts))
        {
            cts.Dispose();
        }
    }

    public bool TryCancel(Guid taskId)
    {
        if (!_running.TryGetValue(taskId, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}

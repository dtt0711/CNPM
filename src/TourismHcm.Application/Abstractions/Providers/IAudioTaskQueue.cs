namespace TourismHcm.Application.Abstractions.Providers;

public interface IAudioTaskQueue
{
    ValueTask EnqueueAsync(Guid taskId, CancellationToken ct = default);
}

using System.Threading.Channels;
using TourismHcm.Application.Abstractions.Providers;

namespace TourismHcm.Infrastructure.Providers.Audio;

/// <summary>
/// In-process, in-memory audio generation task queue backed by a bounded <see cref="Channel{T}"/>.
/// Enqueues task identifiers for the background worker (<see cref="AudioGenerationBackgroundService"/>)
/// to pick up and process via <c>IAudioService.ProcessTaskAsync</c>.
/// </summary>
public sealed class ChannelAudioTaskQueue : IAudioTaskQueue
{
    private readonly Channel<Guid> _channel;

    public ChannelAudioTaskQueue()
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity: 1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            // Single reader fans out work via SemaphoreSlim(MaxParallelTasks) in the background service.
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>
    /// Exposed only for internal consumption by <see cref="AudioGenerationBackgroundService"/>;
    /// not part of the <see cref="IAudioTaskQueue"/> abstraction consumed by Application code.
    /// </summary>
    internal ChannelReader<Guid> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid taskId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(taskId, ct);
}

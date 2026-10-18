using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TourismHcm.Application.Abstractions.Services;

namespace TourismHcm.Infrastructure.Providers.Audio;

/// <summary>
/// Background worker that drains the in-process audio generation queue and processes each
/// task via the scoped <see cref="IAudioService"/>. On startup, it also recovers any task left
/// Pending/Running from a previous process lifetime.
/// </summary>
public sealed class AudioGenerationBackgroundService : BackgroundService
{
    private readonly ChannelAudioTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AudioGenerationBackgroundService> _logger;

    public AudioGenerationBackgroundService(
        ChannelAudioTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AudioGenerationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingTasksAsync(stoppingToken);

        try
        {
            await foreach (var taskId in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessTaskAsync(taskId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown.
        }
    }

    private async Task RecoverPendingTasksAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var audioService = scope.ServiceProvider.GetRequiredService<IAudioService>();
            await audioService.RecoverPendingTasksAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover pending audio generation tasks on startup.");
        }
    }

    private async Task ProcessTaskAsync(Guid taskId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var audioService = scope.ServiceProvider.GetRequiredService<IAudioService>();
            await audioService.ProcessTaskAsync(taskId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while processing audio generation task {TaskId}", taskId);
        }
    }
}

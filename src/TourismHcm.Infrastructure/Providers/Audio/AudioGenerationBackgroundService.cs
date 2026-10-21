using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Options;

namespace TourismHcm.Infrastructure.Providers.Audio;

/// <summary>
/// Background worker that drains the audio generation queue with up to
/// <see cref="AudioOptions.MaxParallelTasks"/> concurrent TTS jobs (default 3).
/// </summary>
public sealed class AudioGenerationBackgroundService : BackgroundService
{
    private readonly ChannelAudioTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AudioOptions _options;
    private readonly ILogger<AudioGenerationBackgroundService> _logger;

    public AudioGenerationBackgroundService(
        ChannelAudioTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<AudioOptions> options,
        ILogger<AudioGenerationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingTasksAsync(stoppingToken);

        var parallelism = Math.Clamp(_options.MaxParallelTasks, 1, 8);
        using var gate = new SemaphoreSlim(parallelism, parallelism);
        var inFlight = new List<Task>();

        try
        {
            await foreach (var taskId in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await gate.WaitAsync(stoppingToken);
                var capturedId = taskId;
                var work = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessTaskAsync(capturedId, stoppingToken);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, stoppingToken);

                inFlight.Add(work);
                inFlight.RemoveAll(t => t.IsCompleted);
            }

            await Task.WhenAll(inFlight);
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

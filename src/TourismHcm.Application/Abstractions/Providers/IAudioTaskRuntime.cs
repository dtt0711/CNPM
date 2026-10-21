namespace TourismHcm.Application.Abstractions.Providers;

/// <summary>
/// Tracks in-flight TTS work so Pause/Cancel can abort synthesis mid-flight.
/// </summary>
public interface IAudioTaskRuntime
{
    CancellationToken Register(Guid taskId, CancellationToken outerCt);

    void Unregister(Guid taskId);

    bool TryCancel(Guid taskId);
}

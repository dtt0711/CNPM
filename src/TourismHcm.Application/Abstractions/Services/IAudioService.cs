using TourismHcm.Contracts.Audio;

namespace TourismHcm.Application.Abstractions.Services;

public interface IAudioService
{
    /// <summary>
    /// Ensures the <paramref name="lang"/> translation for a place is up to date with the
    /// current Vietnamese source text, re-translating only when the content hash changed and
    /// the existing translation (if any) was not manually edited.
    /// </summary>
    Task EnsureTranslationAsync(Guid placeId, string lang, CancellationToken ct = default);

    /// <summary>
    /// Ensures a translation exists and enqueues a TTS generation task if the audio is missing
    /// or stale. Returns the current/latest task for the place+lang pair.
    /// </summary>
    Task<AudioTaskDto> RequestAudioAsync(Guid placeId, string lang, CancellationToken ct = default);

    /// <summary>
    /// Streams the already-generated, ready audio asset for a specific place+lang pair only.
    /// </summary>
    Task<TtsStreamResult> StreamAsync(Guid placeId, string lang, CancellationToken ct = default);

    /// <summary>
    /// Forces regeneration of translations + audio for all priority languages. Used when the
    /// source (Vietnamese) description of a place changes.
    /// </summary>
    Task RegenerateAllLanguagesAsync(Guid placeId, CancellationToken ct = default);

    Task<AudioTaskDto?> GetTaskStatusAsync(Guid placeId, string lang, CancellationToken ct = default);

    /// <summary>
    /// Worker entry point: performs the actual translation lookup + TTS synthesis + storage
    /// for a previously enqueued task.
    /// </summary>
    Task ProcessTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Re-enqueues any tasks left Pending/Running (e.g. after a process restart).
    /// </summary>
    Task RecoverPendingTasksAsync(CancellationToken ct = default);

    Task<AudioPackManifestDto> GetPackManifestAsync(string lang, CancellationToken ct = default);

    Task<AudioTaskDto> CancelTaskAsync(Guid taskId, CancellationToken ct = default);

    Task<AudioTaskDto> PauseTaskAsync(Guid taskId, CancellationToken ct = default);

    Task<AudioTaskDto> ResumeTaskAsync(Guid taskId, CancellationToken ct = default);

    Task<IReadOnlyList<AudioTaskDto>> ListActiveTasksAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AudioTaskDto>> ListRecentTasksAsync(int take = 50, CancellationToken ct = default);
}

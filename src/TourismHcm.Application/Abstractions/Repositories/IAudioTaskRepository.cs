using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface IAudioTaskRepository
{
    Task<AudioGenerationTask?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<AudioGenerationTask?> GetAsync(Guid placeId, string lang, CancellationToken ct = default);

    Task AddAsync(AudioGenerationTask task, CancellationToken ct = default);

    /// <summary>
    /// Returns tasks left in Pending or Running state, used to recover/re-enqueue work after a restart.
    /// </summary>
    Task<IReadOnlyList<AudioGenerationTask>> GetPendingOrRunningAsync(CancellationToken ct = default);

    /// <summary>Active admin queue: Pending, Running, or Paused (includes Place for display).</summary>
    Task<IReadOnlyList<AudioGenerationTask>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Recent tasks for admin stream snapshot (active + recently finished).</summary>
    Task<IReadOnlyList<AudioGenerationTask>> GetRecentAsync(int take, CancellationToken ct = default);
}

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
}

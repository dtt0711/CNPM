using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class AudioTaskRepository : IAudioTaskRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public AudioTaskRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AudioGenerationTask?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.AudioGenerationTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<AudioGenerationTask?> GetAsync(Guid placeId, string lang, CancellationToken ct = default) =>
        _dbContext.AudioGenerationTasks
            .Where(t => t.PlaceId == placeId && t.Lang == lang)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(AudioGenerationTask task, CancellationToken ct = default) =>
        await _dbContext.AudioGenerationTasks.AddAsync(task, ct);

    public async Task<IReadOnlyList<AudioGenerationTask>> GetPendingOrRunningAsync(CancellationToken ct = default) =>
        await _dbContext.AudioGenerationTasks
            .Where(t => t.Status == AudioTaskStatus.Pending || t.Status == AudioTaskStatus.Running)
            .ToListAsync(ct);
}

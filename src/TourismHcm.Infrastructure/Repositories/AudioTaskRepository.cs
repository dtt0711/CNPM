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

    public async Task<IReadOnlyList<AudioGenerationTask>> GetActiveAsync(CancellationToken ct = default) =>
        await _dbContext.AudioGenerationTasks
            .AsNoTracking()
            .Include(t => t.Place)
            .Where(t =>
                t.Status == AudioTaskStatus.Pending
                || t.Status == AudioTaskStatus.Running
                || t.Status == AudioTaskStatus.Paused)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AudioGenerationTask>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        var limit = take <= 0 ? 50 : Math.Min(take, 200);
        return await _dbContext.AudioGenerationTasks
            .AsNoTracking()
            .Include(t => t.Place)
            .OrderByDescending(t => t.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}

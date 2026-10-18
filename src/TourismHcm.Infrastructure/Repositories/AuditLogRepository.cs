using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public AuditLogRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditLog log, CancellationToken ct = default) =>
        await _dbContext.AuditLogs.AddAsync(log, ct);

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> SearchAsync(
        Guid? userId,
        string? entityType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (userId is not null)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}

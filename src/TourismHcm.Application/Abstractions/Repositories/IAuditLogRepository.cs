using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);

    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> SearchAsync(
        Guid? userId,
        string? entityType,
        int page,
        int pageSize,
        CancellationToken ct = default);
}

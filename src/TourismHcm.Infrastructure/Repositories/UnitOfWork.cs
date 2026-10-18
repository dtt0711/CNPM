using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly TourismHcmDbContext _dbContext;

    public UnitOfWork(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _dbContext.SaveChangesAsync(ct);
}

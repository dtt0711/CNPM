using TourismHcm.Contracts.Tours;

namespace TourismHcm.Application.Abstractions.Services;

public interface ITourService
{
    Task<TourDto> CreateAsync(Guid userId, CreateTourRequest request, CancellationToken ct = default);

    Task<TourDto> GetByIdAsync(Guid userId, Guid tourId, CancellationToken ct = default);

    Task<IReadOnlyList<TourDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    Task DeleteAsync(Guid userId, Guid tourId, CancellationToken ct = default);
}

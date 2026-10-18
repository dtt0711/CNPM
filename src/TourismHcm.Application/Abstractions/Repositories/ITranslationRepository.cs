using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface ITranslationRepository
{
    Task<PlaceTranslation?> GetAsync(Guid placeId, string lang, CancellationToken ct = default);

    Task<IReadOnlyList<PlaceTranslation>> GetByPlaceIdAsync(Guid placeId, CancellationToken ct = default);

    Task AddAsync(PlaceTranslation translation, CancellationToken ct = default);

    void Remove(PlaceTranslation translation);
}

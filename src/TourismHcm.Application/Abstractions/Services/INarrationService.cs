using TourismHcm.Contracts.Narration;

namespace TourismHcm.Application.Abstractions.Services;

public interface INarrationService
{
    /// <summary>
    /// Builds narration content for a place, falling back from the requested language to
    /// English and finally Vietnamese when no translation is available.
    /// </summary>
    Task<NarrationResponse> GetNarrationAsync(Guid placeId, string requestedLang, CancellationToken ct = default);
}

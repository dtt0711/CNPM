using TourismHcm.Contracts.Narration;

namespace TourismHcm.Application.Abstractions.Services;

public interface IGeofenceService
{
    /// <summary>
    /// Evaluates the client's current position against known places. Returns null when no
    /// place currently overlaps the position. When a place overlaps but is within its cooldown
    /// window, a response is still returned with <see cref="TriggerResponse.Triggered"/> set to false.
    /// </summary>
    Task<TriggerResponse?> TriggerAsync(TriggerRequest request, CancellationToken ct = default);
}

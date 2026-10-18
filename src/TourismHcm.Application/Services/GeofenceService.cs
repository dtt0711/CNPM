using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Options;
using TourismHcm.Contracts.Narration;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class GeofenceService : IGeofenceService
{
    private readonly IPlaceRepository _placeRepository;
    private readonly INarrationService _narrationService;
    private readonly IDistributedCache _cache;
    private readonly GeofenceOptions _options;

    public GeofenceService(
        IPlaceRepository placeRepository,
        INarrationService narrationService,
        IDistributedCache cache,
        IOptions<GeofenceOptions> options)
    {
        _placeRepository = placeRepository;
        _narrationService = narrationService;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<TriggerResponse?> TriggerAsync(TriggerRequest request, CancellationToken ct = default)
    {
        var (minLat, maxLat, minLng, maxLng) = GeoHelper.BoundingBox(
            request.Latitude, request.Longitude, _options.MaxScanRadiusMeters);

        var candidates = await _placeRepository.GetInBoundingBoxAsync(
            minLat, maxLat, minLng, maxLng, PlaceStatus.Published, ct);

        var overlapping = candidates
            .Select(place => (
                Place: place,
                Distance: GeoHelper.HaversineMeters(request.Latitude, request.Longitude, place.Latitude, place.Longitude),
                Radius: place.TriggerRadiusMeters > 0 ? place.TriggerRadiusMeters : _options.DefaultRadiusMeters))
            .Where(x => x.Distance <= x.Radius)
            .OrderBy(x => x.Distance)
            .ToList();

        if (overlapping.Count == 0)
        {
            return null;
        }

        var nearest = overlapping[0];
        var clientId = string.IsNullOrWhiteSpace(request.ClientId) ? "anonymous" : request.ClientId;
        var cooldownKey = $"cooldown:{clientId}:{nearest.Place.Id}";

        var existingCooldown = await _cache.GetStringAsync(cooldownKey, ct);
        var narration = await _narrationService.GetNarrationAsync(nearest.Place.Id, request.Lang, ct);

        if (existingCooldown is not null)
        {
            return new TriggerResponse
            {
                PlaceId = nearest.Place.Id,
                Name = nearest.Place.Name,
                DistanceMeters = Math.Round(nearest.Distance, 1),
                Triggered = false,
                CooldownActive = true,
                Narration = narration
            };
        }

        await _cache.SetStringAsync(
            cooldownKey,
            DateTimeOffset.UtcNow.ToString("O"),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CooldownMinutes)
            },
            ct);

        return new TriggerResponse
        {
            PlaceId = nearest.Place.Id,
            Name = nearest.Place.Name,
            DistanceMeters = Math.Round(nearest.Distance, 1),
            Triggered = true,
            CooldownActive = false,
            Narration = narration
        };
    }
}

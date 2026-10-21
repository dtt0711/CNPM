using Microsoft.Extensions.Caching.Distributed;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Localization;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class LocalizationService : ILocalizationService
{
    private const int RateLimitPerMinute = 20;
    private readonly IAudioService _audioService;
    private readonly INarrationService _narrationService;
    private readonly IPlaceRepository _placeRepository;
    private readonly IDistributedCache _cache;

    public LocalizationService(
        IAudioService audioService,
        INarrationService narrationService,
        IPlaceRepository placeRepository,
        IDistributedCache cache)
    {
        _audioService = audioService;
        _narrationService = narrationService;
        _placeRepository = placeRepository;
        _cache = cache;
    }

    public async Task<OnDemandLocalizationResult> OnDemandAsync(Guid placeId, string lang, CancellationToken ct = default)
    {
        var normalized = AppLanguages.Normalize(lang);
        if (!await TryAcquireRateLimitAsync($"loc:ondemand:{placeId}:{normalized}", ct))
        {
            return new OnDemandLocalizationResult
            {
                PlaceId = placeId,
                Lang = normalized,
                Status = "rate_limited",
                RetryAfterSeconds = 30,
            };
        }

        var narration = await _narrationService.GetNarrationAsync(placeId, normalized, ct);
        if (narration.Audio.Available && !string.IsNullOrWhiteSpace(narration.Audio.Url))
        {
            return new OnDemandLocalizationResult
            {
                PlaceId = placeId,
                Lang = narration.ActualLang,
                Status = "ready",
                AudioUrl = narration.Audio.Url,
            };
        }

        var task = await _audioService.RequestAudioAsync(placeId, normalized, ct);
        return new OnDemandLocalizationResult
        {
            PlaceId = placeId,
            Lang = normalized,
            Status = task.Status,
            TaskId = task.Id,
        };
    }

    public async Task<WarmupResult> WarmupAsync(string lang, IReadOnlyList<Guid>? placeIds, CancellationToken ct = default)
    {
        var normalized = AppLanguages.Normalize(lang);
        IReadOnlyList<Guid> ids = placeIds is { Count: > 0 }
            ? placeIds
            : (await _placeRepository.GetAllPublishedAsync(ct)).Select(p => p.Id).ToList();

        var queued = 0;
        var ready = 0;
        foreach (var id in ids)
        {
            var narration = await _narrationService.GetNarrationAsync(id, normalized, ct);
            if (narration.Audio.Available && !string.IsNullOrWhiteSpace(narration.Audio.Url))
            {
                ready++;
                continue;
            }

            await _audioService.RequestAudioAsync(id, normalized, ct);
            queued++;
        }

        return new WarmupResult { Lang = normalized, Queued = queued, AlreadyReady = ready };
    }

    public async Task<HotsetResult> PrepareHotsetAsync(double lat, double lng, string lang, CancellationToken ct = default)
    {
        var normalized = AppLanguages.Normalize(lang);
        // ~1.5km bounding box approximation at HCMC latitudes
        const double delta = 0.014;
        var candidates = await _placeRepository.GetInBoundingBoxAsync(
            lat - delta, lat + delta, lng - delta, lng + delta, PlaceStatus.Published, ct);

        var ranked = candidates
            .Select(p => new { Place = p, Distance = GeoHelper.HaversineMeters(lat, lng, p.Latitude, p.Longitude) })
            .Where(x => x.Distance <= 1500)
            .OrderBy(x => x.Distance)
            .Take(10)
            .Select(x => x.Place.Id)
            .ToList();

        var ready = 0;
        foreach (var id in ranked)
        {
            var narration = await _narrationService.GetNarrationAsync(id, normalized, ct);
            if (narration.Audio.Available && !string.IsNullOrWhiteSpace(narration.Audio.Url)
                && !string.IsNullOrWhiteSpace(narration.Text))
            {
                ready++;
                continue;
            }

            await _audioService.RequestAudioAsync(id, normalized, ct);
        }

        return new HotsetResult
        {
            Lang = normalized,
            PlaceIds = ranked,
            ReadyCount = ready,
            IsReady = ready >= Math.Min(3, ranked.Count),
        };
    }

    private async Task<bool> TryAcquireRateLimitAsync(string key, CancellationToken ct)
    {
        var cacheKey = $"rl:{key}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var raw = await _cache.GetStringAsync(cacheKey, ct);
        var count = int.TryParse(raw, out var n) ? n : 0;
        if (count >= RateLimitPerMinute)
        {
            return false;
        }

        await _cache.SetStringAsync(
            cacheKey,
            (count + 1).ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) },
            ct);
        return true;
    }
}

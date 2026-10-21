using Microsoft.Extensions.Logging;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Narration;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Application.Services;

public sealed class NarrationService : INarrationService
{
    private readonly IPlaceRepository _placeRepository;
    private readonly ITranslationRepository _translationRepository;
    private readonly IAudioAssetRepository _audioAssetRepository;
    private readonly IAudioStorage _audioStorage;
    private readonly IAudioService _audioService;
    private readonly ILogger<NarrationService> _logger;

    public NarrationService(
        IPlaceRepository placeRepository,
        ITranslationRepository translationRepository,
        IAudioAssetRepository audioAssetRepository,
        IAudioStorage audioStorage,
        IAudioService audioService,
        ILogger<NarrationService> logger)
    {
        _placeRepository = placeRepository;
        _translationRepository = translationRepository;
        _audioAssetRepository = audioAssetRepository;
        _audioStorage = audioStorage;
        _audioService = audioService;
        _logger = logger;
    }

    public async Task<NarrationResponse> GetNarrationAsync(Guid placeId, string requestedLang, CancellationToken ct = default)
    {
        var place = await _placeRepository.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        var normalizedRequested = AppLanguages.Normalize(requestedLang);

        // Ensure machine translation exists for the requested language (best-effort).
        if (!string.Equals(normalizedRequested, AppLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _audioService.EnsureTranslationAsync(placeId, normalizedRequested, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Auto-translate failed for place {PlaceId} lang {Lang}", placeId, normalizedRequested);
            }
        }

        var fallbackChain = new[] { normalizedRequested, AppLanguages.English, AppLanguages.Vietnamese }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string? actualLang = null;
        string? text = null;

        foreach (var candidate in fallbackChain)
        {
            var candidateText = await ResolveTextAsync(place, candidate, ct);
            if (!string.IsNullOrWhiteSpace(candidateText))
            {
                actualLang = candidate;
                text = candidateText;
                break;
            }
        }

        actualLang ??= AppLanguages.Vietnamese;
        text ??= place.DescriptionVi;

        // Kick off TTS generation when audio is missing for the language we will speak.
        try
        {
            await _audioService.RequestAudioAsync(placeId, actualLang, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Auto-request audio failed for place {PlaceId} lang {Lang}", placeId, actualLang);
        }

        var audioInfo = await BuildAudioInfoAsync(placeId, actualLang, ct);

        return new NarrationResponse
        {
            PlaceId = place.Id,
            Name = place.Name,
            RequestedLang = normalizedRequested,
            ActualLang = actualLang,
            IsFallback = !string.Equals(actualLang, normalizedRequested, StringComparison.OrdinalIgnoreCase),
            Text = text,
            Audio = audioInfo
        };
    }

    private async Task<string?> ResolveTextAsync(Place place, string lang, CancellationToken ct)
    {
        if (string.Equals(lang, AppLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
        {
            return place.DescriptionVi;
        }

        var translation = await _translationRepository.GetAsync(place.Id, lang, ct);
        return translation?.Text;
    }

    private async Task<AudioInfoDto> BuildAudioInfoAsync(Guid placeId, string lang, CancellationToken ct)
    {
        var asset = await _audioAssetRepository.GetAsync(placeId, lang, ct);
        var task = await _audioService.GetTaskStatusAsync(placeId, lang, ct);

        if (asset is null)
        {
            return new AudioInfoDto
            {
                Available = false,
                Status = task?.Status ?? "Pending"
            };
        }

        if (asset.Status != AudioAssetStatus.Ready)
        {
            return new AudioInfoDto
            {
                Available = false,
                Status = task?.Status ?? asset.Status.ToString(),
                DurationSec = asset.DurationSec
            };
        }

        return new AudioInfoDto
        {
            Available = true,
            Url = _audioStorage.GetPublicUrl(asset.Path),
            DurationSec = asset.DurationSec,
            Status = asset.Status.ToString()
        };
    }
}

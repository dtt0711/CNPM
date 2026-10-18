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

    public NarrationService(
        IPlaceRepository placeRepository,
        ITranslationRepository translationRepository,
        IAudioAssetRepository audioAssetRepository,
        IAudioStorage audioStorage)
    {
        _placeRepository = placeRepository;
        _translationRepository = translationRepository;
        _audioAssetRepository = audioAssetRepository;
        _audioStorage = audioStorage;
    }

    public async Task<NarrationResponse> GetNarrationAsync(Guid placeId, string requestedLang, CancellationToken ct = default)
    {
        var place = await _placeRepository.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        var normalizedRequested = AppLanguages.Normalize(requestedLang);

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

        // Vietnamese description is always present per place invariants; this should not happen,
        // but guard defensively instead of throwing a confusing null-reference later.
        actualLang ??= AppLanguages.Vietnamese;
        text ??= place.DescriptionVi;

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

        if (asset is null)
        {
            return new AudioInfoDto
            {
                Available = false,
                Status = "NotRequested"
            };
        }

        if (asset.Status != AudioAssetStatus.Ready)
        {
            return new AudioInfoDto
            {
                Available = false,
                Status = asset.Status.ToString(),
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

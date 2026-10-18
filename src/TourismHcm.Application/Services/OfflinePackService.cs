using System.Text;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Offline;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class OfflinePackService : IOfflinePackService
{
    private readonly IPlaceRepository _placeRepository;
    private readonly ITranslationRepository _translationRepository;
    private readonly IAudioAssetRepository _audioAssetRepository;
    private readonly IAudioStorage _audioStorage;

    public OfflinePackService(
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

    public async Task<OfflinePackManifest> BuildManifestAsync(IEnumerable<string>? langs = null, CancellationToken ct = default)
    {
        var requestedLangs = (langs?.Any() == true ? langs : AppLanguages.Priority)
            .Select(AppLanguages.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var places = await _placeRepository.GetAllPublishedAsync(ct);
        var items = new List<OfflinePlaceItem>(places.Count);
        var hashBuilder = new StringBuilder();

        foreach (var place in places)
        {
            var texts = new Dictionary<string, string>();
            var audioUrls = new Dictionary<string, string>();

            hashBuilder.Append(place.Id).Append(place.ContentHash);

            foreach (var lang in requestedLangs)
            {
                string? text;
                if (string.Equals(lang, AppLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
                {
                    text = place.DescriptionVi;
                }
                else
                {
                    var translation = await _translationRepository.GetAsync(place.Id, lang, ct);
                    text = translation?.Text;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    texts[lang] = text;
                }

                var asset = await _audioAssetRepository.GetAsync(place.Id, lang, ct);
                if (asset is not null && asset.Status == AudioAssetStatus.Ready)
                {
                    audioUrls[lang] = _audioStorage.GetPublicUrl(asset.Path);
                    hashBuilder.Append(asset.ContentHash);
                }
            }

            items.Add(new OfflinePlaceItem
            {
                PlaceId = place.Id,
                Name = place.Name,
                Category = place.Category.ToString(),
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                TriggerRadiusMeters = place.TriggerRadiusMeters,
                Texts = texts,
                AudioUrls = audioUrls
            });
        }

        var version = ContentHash.Compute(hashBuilder.ToString());

        return new OfflinePackManifest
        {
            Version = version,
            GeneratedAt = DateTimeOffset.UtcNow,
            Places = items
        };
    }
}

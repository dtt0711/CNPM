using Mapster;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class PlaceService : IPlaceService
{
    private readonly IPlaceRepository _placeRepository;
    private readonly IAudioAssetRepository _audioAssetRepository;
    private readonly IAudioService _audioService;
    private readonly IUnitOfWork _unitOfWork;

    public PlaceService(
        IPlaceRepository placeRepository,
        IAudioAssetRepository audioAssetRepository,
        IAudioService audioService,
        IUnitOfWork unitOfWork)
    {
        _placeRepository = placeRepository;
        _audioAssetRepository = audioAssetRepository;
        _audioService = audioService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlaceDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var place = await _placeRepository.GetByIdWithTranslationsAsync(id, ct)
            ?? throw new NotFoundException("Place", id);

        return await MapToDetailAsync(place, ct);
    }

    public async Task<PagedResult<PlaceDto>> SearchAsync(PlaceQueryRequest query, CancellationToken ct = default)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize is <= 0 or > 100 ? 20 : query.PageSize;

        PlaceCategory? category = null;
        if (!string.IsNullOrWhiteSpace(query.Category) && Enum.TryParse<PlaceCategory>(query.Category, true, out var parsedCategory))
        {
            category = parsedCategory;
        }

        PlaceStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<PlaceStatus>(query.Status, true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        var (items, totalCount) = await _placeRepository.SearchAsync(
            query.Search,
            category,
            query.District,
            status,
            query.SortBy,
            query.Descending,
            page,
            pageSize,
            ct);

        var dtos = items.Adapt<List<PlaceDto>>();
        return PagedResult<PlaceDto>.Create(dtos, page, pageSize, totalCount);
    }

    public async Task<PlaceDetailDto> CreateAsync(CreatePlaceRequest request, CancellationToken ct = default)
    {
        var category = ParseCategory(request.Category);
        var now = DateTimeOffset.UtcNow;

        var place = new Place
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            DescriptionVi = request.DescriptionVi.Trim(),
            ContentHash = ContentHash.Compute(request.DescriptionVi.Trim()),
            Category = category,
            District = request.District.Trim(),
            Address = request.Address.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TriggerRadiusMeters = request.TriggerRadiusMeters is > 0 ? request.TriggerRadiusMeters.Value : 30,
            OpeningHours = request.OpeningHours,
            TicketPrice = request.TicketPrice,
            ImageUrl = request.ImageUrl,
            Tags = request.Tags,
            Status = PlaceStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _placeRepository.AddAsync(place, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await MapToDetailAsync(place, ct);
    }

    public async Task<PlaceDetailDto> UpdateAsync(Guid id, UpdatePlaceRequest request, CancellationToken ct = default)
    {
        var place = await _placeRepository.GetByIdWithTranslationsAsync(id, ct)
            ?? throw new NotFoundException("Place", id);

        var descriptionChanged = false;

        if (request.Name is not null)
        {
            place.Name = request.Name.Trim();
        }

        if (request.DescriptionVi is not null)
        {
            var trimmed = request.DescriptionVi.Trim();
            var newHash = ContentHash.Compute(trimmed);
            if (!string.Equals(newHash, place.ContentHash, StringComparison.Ordinal))
            {
                place.DescriptionVi = trimmed;
                place.ContentHash = newHash;
                descriptionChanged = true;
            }
        }

        if (request.Category is not null)
        {
            place.Category = ParseCategory(request.Category);
        }

        if (request.District is not null)
        {
            place.District = request.District.Trim();
        }

        if (request.Address is not null)
        {
            place.Address = request.Address.Trim();
        }

        if (request.Latitude is not null)
        {
            place.Latitude = request.Latitude.Value;
        }

        if (request.Longitude is not null)
        {
            place.Longitude = request.Longitude.Value;
        }

        if (request.TriggerRadiusMeters is > 0)
        {
            place.TriggerRadiusMeters = request.TriggerRadiusMeters.Value;
        }

        if (request.OpeningHours is not null)
        {
            place.OpeningHours = request.OpeningHours;
        }

        if (request.TicketPrice is not null)
        {
            place.TicketPrice = request.TicketPrice;
        }

        if (request.ImageUrl is not null)
        {
            place.ImageUrl = request.ImageUrl;
        }

        if (request.Tags is not null)
        {
            place.Tags = request.Tags;
        }

        if (request.Status is not null && Enum.TryParse<PlaceStatus>(request.Status, true, out var status))
        {
            place.Status = status;
        }

        place.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        if (descriptionChanged)
        {
            await _audioService.RegenerateAllLanguagesAsync(place.Id, ct);
        }

        return await MapToDetailAsync(place, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var place = await _placeRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Place", id);

        _placeRepository.Remove(place);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NearbyPlaceDto>> GetNearbyAsync(
        double latitude, double longitude, double radiusMeters, int? limit, CancellationToken ct = default)
    {
        var (minLat, maxLat, minLng, maxLng) = GeoHelper.BoundingBox(latitude, longitude, radiusMeters);

        var candidates = await _placeRepository.GetInBoundingBoxAsync(
            minLat, maxLat, minLng, maxLng, PlaceStatus.Published, ct);

        var withDistance = candidates
            .Select(place => (Place: place, Distance: GeoHelper.HaversineMeters(latitude, longitude, place.Latitude, place.Longitude)))
            .Where(x => x.Distance <= radiusMeters)
            .OrderBy(x => x.Distance)
            .ToList();

        if (limit is > 0)
        {
            withDistance = withDistance.Take(limit.Value).ToList();
        }

        return withDistance
            .Select(x =>
            {
                var dto = x.Place.Adapt<NearbyPlaceDto>();
                return dto with { DistanceMeters = Math.Round(x.Distance, 1) };
            })
            .ToList();
    }

    private async Task<PlaceDetailDto> MapToDetailAsync(Place place, CancellationToken ct)
    {
        var dto = place.Adapt<PlaceDetailDto>();
        var audioAssets = await _audioAssetRepository.GetByPlaceIdAsync(place.Id, ct);
        var availableLangs = audioAssets
            .Where(a => a.Status == AudioAssetStatus.Ready)
            .Select(a => a.Lang)
            .ToList();

        return dto with { AvailableAudioLanguages = availableLangs };
    }

    private static PlaceCategory ParseCategory(string category)
    {
        if (!Enum.TryParse<PlaceCategory>(category, true, out var parsed))
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(category)] = [$"Unknown category '{category}'."]
            });
        }

        return parsed;
    }
}
